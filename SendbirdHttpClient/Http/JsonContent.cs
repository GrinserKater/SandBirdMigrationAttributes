﻿using System.Net.Http;
using System.Text;

namespace SendbirdHttpClient.Http
{
	public sealed class JsonContent : StringContent
	{
		public JsonContent(string content) : base(content, Encoding.UTF8, "application/json")
		{
		}
	}
}
