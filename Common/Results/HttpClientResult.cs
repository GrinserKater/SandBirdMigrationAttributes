using System;
using System.Net;

namespace Common.Results
{
	public class HttpClientResult<T> where T : class
	{
		public HttpStatusCode HttpStatusCode { get; }

		public string FormattedMessage { get; }

		public string OriginalMessage { get; } 

		public T Payload { get; }

		public bool IsSuccess => HttpStatusCode >= HttpStatusCode.OK && HttpStatusCode < HttpStatusCode.MultipleChoices;

		public HttpClientResult(HttpStatusCode httpStatusCode, string message) : this(httpStatusCode, message, null, null) { }

		public HttpClientResult(HttpStatusCode httpStatusCode, string message, string originalMessage) : this(httpStatusCode, message, originalMessage, null) { }

		public HttpClientResult(HttpStatusCode httpStatusCode, T payload) : this(httpStatusCode, null, null, payload) { }

		private HttpClientResult(HttpStatusCode httpStatusCode, string message, string originalMessage, T payload)
		{
			HttpStatusCode   = httpStatusCode;
			FormattedMessage = message;
			OriginalMessage  = originalMessage;
			Payload          = payload;
		}

		public HttpClientResult<TResult> ShallowCopy<TResult>() where TResult : class
		{
			return new HttpClientResult<TResult>(HttpStatusCode, FormattedMessage, OriginalMessage);
		}

		public HttpClientResult<TResult> Convert<TResult>(Func<T, TResult> converter) where TResult : class
		{
			if (converter == null)
				throw new ArgumentNullException(nameof(converter));

			TResult result = converter(Payload);

			return new HttpClientResult<TResult>(HttpStatusCode, FormattedMessage, OriginalMessage, result);
		}
	}
}
