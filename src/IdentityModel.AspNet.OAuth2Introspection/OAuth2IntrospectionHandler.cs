﻿// Copyright (c) Dominick Baier & Brock Allen. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using IdentityModel.Client;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Authentication;

namespace IdentityModel.AspNet.OAuth2Introspection
{
    public class OAuth2IntrospectionHandler : AuthenticationHandler<OAuth2IntrospectionOptions>
    {
        private readonly IntrospectionClient _client;

        public OAuth2IntrospectionHandler(IntrospectionClient client)
        {
            _client = client;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string token = Options.TokenRetriever(Context.Request);

            if (token.IsMissing())
            {
                return AuthenticateResult.Failed("No bearer token.");
            }

            if (token.Contains('.') && Options.SkipTokensWithDots)
            {
                return AuthenticateResult.Failed("Token contains a dot. Skipping.");
            }

            var response = await _client.SendAsync(new IntrospectionRequest
            {
                Token = token,
                ClientId = Options.ScopeName,
                ClientSecret = Options.ScopeSecret
            });

            if (response.IsError)
            {
                return AuthenticateResult.Failed("Error returned from introspection: " + response.Error);
            }

            if (response.IsActive)
            {
                var claims = new List<Claim>(response.Claims
                    .Where(c => c.Item1 != "active")
                    .Select(c => new Claim(c.Item1, c.Item2)));

                if (Options.SaveTokensAsClaims)
                {
                    claims.Add(new Claim("access_token", token));
                }

                var id = new ClaimsIdentity(claims, Options.AuthenticationScheme, Options.NameClaimType, Options.RoleClaimType);
                var principal = new ClaimsPrincipal(id);

                var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Options.AuthenticationScheme);
                return AuthenticateResult.Success(ticket);
            }

            return AuthenticateResult.Failed("invalid token.");
        }
    }
}