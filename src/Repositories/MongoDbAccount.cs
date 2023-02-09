﻿using Bulwark.Auth.Repositories.Exception;
using Bulwark.Auth.Repositories.Model;

namespace Bulwark.Auth.Repositories;

public class MongoDbAccount : IAccountRepository
{
    private readonly IEncrypt _encrypt;
    private readonly IMongoCollection<AccountModel> _accountCollection;
    private readonly IMongoCollection<VerificationModel>
        _verificationCollection;
    private readonly IMongoCollection<ForgotModel> _forgotCollection;
    
    public MongoDbAccount(IMongoDatabase db, IEncrypt encrypt)
    {
        _accountCollection = db.GetCollection<AccountModel>("account");
        _verificationCollection =
            db.GetCollection<VerificationModel>("verification");
        _forgotCollection = db.GetCollection<ForgotModel>("forgot");
        _encrypt = encrypt;

        CreateIndexes();
    }

    public async Task<VerificationModel> Create(string email, string password)
    {
        try
        {
            var newAccount = new AccountModel
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Email = email,
                Password = _encrypt.Encrypt(password),
                Salt = Guid.NewGuid().ToString(),
                IsVerified = false,
                IsEnabled = false,
                IsDeleted = false,
                Created = DateTime.Now,
                Modified = DateTime.Now
            };

            var verification = new VerificationModel(email,
                Guid.NewGuid().ToString());
            await _accountCollection.InsertOneAsync(newAccount);
            await _verificationCollection.InsertOneAsync(verification);

            return verification;
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                throw new BulwarkDbDuplicateException($"{email} already exists");
            }
            throw new BulwarkDbException("Error creating account", e);
        }
    }

    public async Task Verify(string email, string verificationToken)
    {
        var verificationDeleteResult = await _verificationCollection
            .DeleteOneAsync(v => v.Email == email && v.Token == verificationToken);

        if (verificationDeleteResult.DeletedCount == 1)
        {
            var update = Builders<AccountModel>.Update
                .Set(p => p.IsVerified, true)
                .Set(p => p.IsEnabled, true)
                .Set(p => p.Modified, DateTime.Now);

            var result = await _accountCollection.UpdateOneAsync(p =>
            p.Email == email,
                update);

            if(result.ModifiedCount != 1)
            {
                throw new BulwarkDbException($"Account: {email} cannot be verified");
            }
        }
        else
        {
            throw new BulwarkDbException("Account - unknown error");
        }
    }

    public async Task<AccountModel> GetAccount(string email)
    {
        try{
            var account = await _accountCollection
                .Find(a => a.Email == email)
                .FirstOrDefaultAsync();

            if (account == null)
            {
                throw new BulwarkDbNotFoundException($"Account: {email} not found");
            }

            return account;
        }
        catch (MongoException e)
        {
            throw new BulwarkDbException($"Error getting account: {email}", e);
        }
    }

    public async Task Delete(string email)
    {
        var update = Builders<AccountModel>.Update
                .Set(p => p.IsVerified, false)
                .Set(p => p.IsEnabled, false)
                .Set(p => p.IsDeleted, true)
                .Set(p => p.Modified, DateTime.Now);

        var result = await _accountCollection.
            UpdateOneAsync(a => a.Email == email, update);

        if(result.ModifiedCount != 1)
        {
            throw
                new BulwarkDbException($"Account {email} could not be deleted");
        }
    }

    public async Task Disable(string email)
    {
        var update = Builders<AccountModel>.Update
                .Set(p => p.IsEnabled, false)
                .Set(p => p.Modified, DateTime.Now);

        var result = await _accountCollection.
            UpdateOneAsync(a => a.Email == email, update);

        if (result.ModifiedCount != 1)
        {
            throw
                new BulwarkDbException("Account could not be disabled");
        }
    }

    public async Task Enable(string email)
    {
        var update = Builders<AccountModel>.Update
                .Set(p => p.IsEnabled, true)
                .Set(p => p.Modified, DateTime.Now);

        var result = await _accountCollection.
            UpdateOneAsync(a => a.Email == email, update);

        if (result.ModifiedCount != 1)
        {
            throw
                new BulwarkDbException("Account could not be enabled");
        }
    }

    /// <summary>
    /// Changes a user email 
    /// </summary>
    /// <param name="email"></param>
    /// <param name="newEmail"></param>
    /// <returns></returns>
    /// <exception cref="BulwarkDbException"></exception>
    /// <exception cref="BulwarkDbDuplicateException"></exception>
    public async Task ChangeEmail(string email, string newEmail)
    {
        try
        {
            var update = Builders<AccountModel>.Update
                    .Set(p => p.Email, newEmail)
                    .Set(p => p.Modified, DateTime.Now);

            var result = await _accountCollection.
                UpdateOneAsync(a => a.Email == email, update);

            if (result.ModifiedCount != 1)
            {
                throw
                    new BulwarkDbException($"Email: {email} could not be found");
            }
        }
        catch(MongoWriteException exception)
        {
            throw
                new BulwarkDbDuplicateException($"Email: {newEmail} in use",
                exception);
        }
    }

    public async Task ChangePassword(string email, string newPassword)
    {
        var update = Builders<AccountModel>.Update
                .Set(p => p.Password, _encrypt.Encrypt(newPassword))
                .Set(p => p.Modified, DateTime.Now);

        var result = await _accountCollection.
            UpdateOneAsync(a => a.Email == email, update);

        if (result.ModifiedCount != 1)
        {
            throw
                new BulwarkDbException($"Account: {email} password could not be updated");
        }
    }

    public async Task<ForgotModel> ForgotPassword(string email)
    {
        var forgot = new ForgotModel(email,
            Guid.NewGuid().ToString());

        await _forgotCollection.InsertOneAsync(forgot);

        return forgot;
    }

    public async Task ResetPasswordWithToken(string email,
        string forgotToken, string newPassword)
    {
        var forgotDeleteResult = await _forgotCollection
            .DeleteOneAsync(v => v.Email == email && v.Token == forgotToken);

        if (forgotDeleteResult.DeletedCount != 1) throw new BulwarkDbException("Reset token invalid");
        var update = Builders<AccountModel>.Update
            .Set(p => p.Password, _encrypt.Encrypt(newPassword))
            .Set(p => p.Modified, DateTime.Now);

        var result = await _accountCollection.
            UpdateOneAsync(a => a.Email == email, update);

        if (result.ModifiedCount != 1)
        {
            throw
                new BulwarkDbException("Password could not be reset");
        }
    }

    private void CreateIndexes()
    {
        var indexKeysDefine = Builders<AccountModel>
            .IndexKeys
            .Ascending(indexKey => indexKey.Email);

        _accountCollection.Indexes.CreateOne(
            new CreateIndexModel<AccountModel>(indexKeysDefine,
            new CreateIndexOptions() { Unique = true, Name = "Email_Unique" }));
    }

    /// <summary>
    /// Look into when they change there email on a social
    /// </summary>
    /// <param name="email"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public async Task LinkSocial(string email, SocialProvider provider)
    {
        var account = await GetAccount(email);
        account.SocialProviders.Add(provider);

        var update = Builders<AccountModel>.Update
                .Set(p => p.SocialProviders, account.SocialProviders)
                .Set(p => p.Modified, DateTime.Now);

        var result = await _accountCollection.
            UpdateOneAsync(a => a.Email == email, update);

        if (result.ModifiedCount != 1)
        {
            throw
                new BulwarkDbException("Cannot link social account");
        }
    }
}

