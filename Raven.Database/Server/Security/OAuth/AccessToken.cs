﻿using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json;
using Raven.Abstractions;
using Raven.Json.Linq;

namespace Raven.Database.Server.Security.OAuth
{
	public class AccessToken
	{
		public string Body { get; set; }
		public string Signature { get; set; }

		private bool MatchesSignature(X509Certificate2 cert)
		{
			var csp = (RSACryptoServiceProvider)cert.PublicKey.Key;

			var signatureData = Convert.FromBase64String(Signature);
			var bodyData = Encoding.Unicode.GetBytes(Body);

			using (var hasher = new SHA1Managed())
			{
				var hash = hasher.ComputeHash(bodyData);

				return csp.VerifyHash(hash, CryptoConfig.MapNameToOID("SHA1"), signatureData);
			}
		}

		public static bool TryParseBody(X509Certificate2 cert, string token, out AccessTokenBody body)
		{
			AccessToken accessToken;
			if (TryParse(token, out accessToken) == false)
			{
				body = null;
				return false;
			}

			if (accessToken.MatchesSignature(cert) == false)
			{
				body = null;
				return false;
			}

			try
			{
				body = JsonConvert.DeserializeObject<AccessTokenBody>(accessToken.Body);
				return true;
			}
			catch
			{
				body = null;
				return false;
			}
		}

		private static bool TryParse(string token, out AccessToken accessToken)
		{
			try
			{
				accessToken = JsonConvert.DeserializeObject<AccessToken>(token);
				return true;
			}
			catch
			{
				accessToken = null;
				return false;
			}
		}

		public static AccessToken Create(X509Certificate2 cert, string userId, string[] databases)
		{
			var issued = (SystemTime.UtcNow - DateTime.MinValue).TotalMilliseconds;

			var body = RavenJObject.FromObject(new AccessTokenBody { UserId = userId, AuthorizedDatabases = databases ?? new string[0], Issued = issued })
					.ToString(Formatting.None);

			var signature = Sign(body, cert);

			return new AccessToken { Body = body, Signature = signature };
		}

		static string Sign(string body, X509Certificate2 cert)
		{
			var csp = (RSACryptoServiceProvider)cert.PrivateKey;

			var data = Encoding.Unicode.GetBytes(body);
			using (var hasher = new SHA1Managed())
			{
				var hash = hasher.ComputeHash(data);
				return Convert.ToBase64String(csp.SignHash(hash, CryptoConfig.MapNameToOID("SHA1")));
			}
		}

		public string Serialize()
		{
			return RavenJObject.FromObject(this).ToString(Formatting.None);
		}

	}
}