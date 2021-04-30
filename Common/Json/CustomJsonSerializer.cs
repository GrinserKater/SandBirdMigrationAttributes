using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Common.Json
{
	public static class CustomJsonSerializer
	{
		public static T  DeserializeFromString<T>(string json) where T : class
		{
			if(String.IsNullOrWhiteSpace(json)) return null;

			T result = JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings
			{
				DefaultValueHandling = DefaultValueHandling.Populate,
				NullValueHandling    = NullValueHandling.Ignore,
				ContractResolver     = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
				Error                = (s, e) =>
				{
					Trace.WriteLine($"Could not deserialise JSON to to the object of type {typeof(T)}.");
					Debug.WriteLine($"JSON [{json}]. Exception: [{e.ErrorContext.Error.Message}]");
					e.ErrorContext.Handled = true;
				}


			});

			return result;
		}

		public static string Serialize<T>(T @object)
		{
			if (@object == null) return String.Empty;

			string result = JsonConvert.SerializeObject(@object, new JsonSerializerSettings
			{
				DefaultValueHandling = DefaultValueHandling.Populate,
				NullValueHandling    = NullValueHandling.Ignore,
				ContractResolver     = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
				Error                = (s, e) =>
				{
					Trace.WriteLine($"Could not serialise object [{e.CurrentObject}]. Exception: [{e.ErrorContext.Error.Message}]");
					e.ErrorContext.Handled = true;
				}

			});

			return result;
		}
	}
}
