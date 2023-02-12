﻿using System.Threading.Tasks;
using Bulwark.Auth.Core.Domain;
using Bulwark.Repositories.Util;
using Bulwark.Auth.Core.Exception;
using Bulwark.Auth.Repositories;
using Bulwark.Auth.Repositories.Exception;
using Bulwark.Auth.Repositories.Model;

namespace Bulwark.Auth.Core;

public class AuthenticationManager : IAuthenticationManager
{
    private readonly TokenStrategyContext _tokenStrategy;
    private readonly IAccountRepository _accountRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IEncrypt _encrypt;

    public AuthenticationManager(
        ICertManager certManager,
        ITokenRepository tokenRepository,
        IEncrypt encrypt,
        IAccountRepository accountRepository,
        IAuthorizationRepository authorizationRepository)
    {
        _tokenStrategy = certManager.TokenContext;
        _accountRepository = accountRepository;
        _tokenRepository = tokenRepository;
        _authorizationRepository = authorizationRepository;
        _encrypt = encrypt;
    }

    /// <summary>
    /// Classic authentication, user/password if successfully authenticated tokens are returned to
    /// client. The authenticated tokens must be acknowledged by the client before proceeding 
    /// </summary>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <param name="tokenizerName"></param>
    /// <returns>Authenticated</returns>
    /// <exception cref="BulwarkAuthenticationException"></exception>
    public async Task<Authenticated> Authenticate(string email,
        string password, string tokenizerName = "default")
    {
        try
        {
            var account = await _accountRepository.GetAccount(email);

            CheckAccountHealth(account);
            if (account == null || !_encrypt.Verify(password, account.Password))
                throw
                    new BulwarkAuthenticationException("Account cannot be authenticated");
            var roles = await _authorizationRepository.ReadAccountRoles(account.Id);
            var permissions = await _authorizationRepository.ReadAccountPermissions(account.Id);
            return Util.Authenticate
                .CreateTokens(account, roles, permissions,
                    _tokenStrategy.GetTokenizer(tokenizerName));
        }
        catch (BulwarkDbException exception)
        {
            throw new BulwarkAuthenticationException($"Account cannot be authenticated: {email}",
                exception);
        }
    }

   /// <summary>
   /// Acknowledges tokens meaning these tokens are now activated for server side validation
   /// this should always be done after a successful authentication, even if using client side
   /// token validation
   /// </summary>
   /// <param name="authenticated"></param>
   /// <param name="email"></param>
   /// <param name="deviceId"></param>
   /// <exception cref="BulwarkAuthenticationException"></exception>
    public async Task Acknowledge(Authenticated authenticated,
        string email, string deviceId)
    {
        try
        {
            var accountModel = await _accountRepository.GetAccount(email);
            _tokenStrategy.ValidateAccessToken(accountModel.Id,
                authenticated.AccessToken);
            _tokenStrategy.ValidateRefreshToken(accountModel.Id,
                authenticated.RefreshToken);
            await _tokenRepository.Acknowledge(accountModel.Id,
                deviceId,authenticated.AccessToken,
                authenticated.RefreshToken);
        }
        catch(BulwarkDbException exception)
        {
            throw new BulwarkAuthenticationException("Cannot acknowledge tokens", 
                exception);
        }
    }

    /// <summary>
    /// Deep accessToken validation
    /// Use case: when a more in-depth validation is needed, such as when tokens have been revoked
    /// a account is deleted or a account is disabled, validation will fail
    /// this is less performant than Validating Locally with "guard" (Bulwark.Auth client)
    /// but has the advantage of being more secure
    /// </summary>
    /// <param name="email"></param>
    /// <param name="accessToken"></param>
    /// <param name="deviceId"></param>
    /// <returns></returns>
    /// <exception cref="BulwarkTokenException"></exception>
    /// <exception cref="BulwarkAccountException"></exception>
    public async Task<AccessToken> ValidateAccessToken(string email,
        string accessToken, string deviceId)
    {
        try
        {
            var accountModel = await _accountRepository.GetAccount(email);
            CheckAccountHealth(accountModel);
            var acknowledged = await _tokenRepository
                .Get(accountModel.Id,
                    deviceId);

            if (acknowledged == null ||
                acknowledged.AccessToken != accessToken)
            {
                throw new BulwarkTokenException("Token is not acknowledged");
            }

            var token = _tokenStrategy
                .ValidateAccessToken(accountModel.Id, accessToken);

            return token ?? null;
        }
        catch (BulwarkDbException exception)
        {
            throw new BulwarkTokenException("Tokens cannot be validated", exception);
        }
    }
    
    /// <summary>
    /// Renews an acknowledged token when the access token is expired.
    /// Use case: when an account access token expires use the refresh token to get a new set of tokens
    /// </summary>
    /// <param name="email"></param>
    /// <param name="refreshToken"></param>
    /// <param name="deviceId"></param>
    /// <param name="tokenizerName"></param>
    /// <returns></returns>
    /// <exception cref="BulwarkTokenException"></exception>
    /// <exception cref="BulwarkAuthenticationException"></exception>
    public async Task<Authenticated> Renew(string email, string refreshToken,
       string deviceId, string tokenizerName = "default")
    {
        try
        {
            var accountModel = await _accountRepository.GetAccount(email);
            CheckAccountHealth(accountModel);
            var acknowledged = await _tokenRepository
                .Get(accountModel.Id.ToString(), deviceId);

            if (acknowledged.RefreshToken != refreshToken)
            {
                throw new BulwarkTokenException("Refresh token not acknowledged");
            }

            _tokenStrategy.ValidateRefreshToken(accountModel.Id.ToString(),
                refreshToken);
            var roles = await _authorizationRepository
                .ReadAccountRoles(accountModel.Id.ToString());
            var permissions = await _authorizationRepository
                .ReadAccountPermissions(accountModel.Id.ToString());
            var newAccessToken = _tokenStrategy.CreateAccessToken(
                accountModel.Id.ToString(), roles, permissions,
                tokenizerName);
            var newRefreshToken = _tokenStrategy.CreateRefreshToken(
                accountModel.Id.ToString());

            var authenticated = new Authenticated(newAccessToken,
                newRefreshToken);

            return authenticated;
        }
        catch (BulwarkDbException exception)
        {
            throw new BulwarkAuthenticationException("Cannot renew tokens", exception);
        }
    }

 
    /// <summary>
    /// Deletes an acknowledged token.
    /// Use case: logging users out of a device. 
    /// </summary>
    /// <param name="email"></param>
    /// <param name="accessToken"></param>
    /// <param name="deviceId"></param>
    /// <exception cref="BulwarkAuthenticationException"></exception>
    public async Task Revoke(string email, string accessToken,
        string deviceId)
    { 
        if(await ValidateAccessToken(email, accessToken, deviceId) != null)
        {
            try{
                var accountModel = await _accountRepository.GetAccount(email);
                await _tokenRepository.Delete(accountModel.Id, deviceId);
            }
            catch(BulwarkDbException exception)
            {
                throw new BulwarkAuthenticationException("Cannot revoke tokens", exception);
            }
        }
    }

    /// <summary>
    /// Internal method to conviently check if an account is healthy
    /// </summary>
    /// <param name="account"></param>
    /// <exception cref="BulwarkAccountException"></exception>
    private static void CheckAccountHealth(AccountModel account)
    {
        //Keep this order :) 
        if (account.IsDeleted)
        {
            throw new BulwarkAccountException("Account deleted");
        }

        if (!account.IsVerified)
        {
            throw new BulwarkAccountException("Account not verified");
        }

        if (!account.IsEnabled)
        {
            throw new BulwarkAccountException("Account disabled");
        }
    }
}

