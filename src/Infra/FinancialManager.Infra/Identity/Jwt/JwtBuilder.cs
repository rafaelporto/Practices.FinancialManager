﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace FinancialManager.Identity.Jwt
{
    internal class JwtBuilder
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppJwtSettings _appJwtSettings;
        private ApplicationUser _user;
        private ICollection<Claim> _jwtClaims;
        private ClaimsIdentity _identityClaims;

		public JwtBuilder(UserManager<ApplicationUser> userManager, AppJwtSettings appJwtSettings)
		{
            _userManager = userManager ?? 
                throw new ArgumentNullException(nameof(userManager),"Usermanager is required.");
            _appJwtSettings = appJwtSettings ??
                throw new ArgumentNullException(nameof(appJwtSettings), "AppJwtSettings is required.");
        }

        public JwtBuilder WithEmail(string email)
        {
            if (email is null or "") throw new ArgumentException("Email is required.", nameof(email));

            _user = _userManager.FindByEmailAsync(email).Result;
            _jwtClaims = new List<Claim>();
            _identityClaims = new ClaimsIdentity();
            
            _identityClaims.AddClaim(new Claim(JwtRegisteredClaimNames.GivenName, _user.FullName));
            _identityClaims.AddClaim(new Claim(JwtRegisteredClaimNames.Email, _user.Email));

            return this;
        }

        public JwtBuilder WithJwtClaims()
        {
            _jwtClaims.Add(new Claim(JwtRegisteredClaimNames.Sub, _user.Id.ToString()));
            _jwtClaims.Add(new Claim(JwtRegisteredClaimNames.Nbf, ToUnixEpochDate(DateTime.UtcNow).ToString()));
            _jwtClaims.Add(new Claim(JwtRegisteredClaimNames.Iat, ToUnixEpochDate(DateTime.UtcNow).ToString(), ClaimValueTypes.Integer64));

            _identityClaims.AddClaims(_jwtClaims);

            return this;
        }

        public JwtBuilder WithUserRoles()
        {
            if (_user.Roles?.Any() is true)
                foreach (var role in _user.Roles)
                {
                    _identityClaims.AddClaim(new Claim(ClaimTypes.Role, role));
                }

            return this;
        }

        public string BuildToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appJwtSettings.SecretKey);
            var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _appJwtSettings.Issuer,
                Audience = _appJwtSettings.Audience,
                Subject = _identityClaims,
                Expires = DateTime.UtcNow.AddHours(_appJwtSettings.Expiration),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            });

            return tokenHandler.WriteToken(token);
        }

        public UserResponse BuildUserResponse()
        {
            var user = new UserResponse
            {
                AccessToken = BuildToken(),
                ExpiresIn = DateTimeOffset.UtcNow.AddHours(_appJwtSettings.Expiration),
                UserToken = new UserToken
                {
                    Email = _user.Email,
                    Roles = _user.Roles
                }
            };

            return user;
        }

        private static long ToUnixEpochDate(DateTime date)
            => (long)Math.Round((date.ToUniversalTime() - DateTimeOffset.UnixEpoch).TotalSeconds);
    }
}
