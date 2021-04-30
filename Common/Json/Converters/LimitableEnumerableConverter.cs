using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Common.Json.Converters
{
	public class LimitableEnumerableConverter<T> : JsonConverter<IEnumerable<T>>
	{
		private readonly int _maxJsonLength = Int32.MaxValue;

		public LimitableEnumerableConverter() { }

		public LimitableEnumerableConverter(int maxJsonLength)
		{
			if (maxJsonLength > 0) _maxJsonLength = maxJsonLength;
		}

		public override void WriteJson(JsonWriter writer, IEnumerable<T> value, JsonSerializer serializer)
		{
			var array = JArray.FromObject(value);

			if (_maxJsonLength == Int32.MaxValue)
			{
				serializer.Serialize(writer, array);
				return;
			}

			int totalLength;
			int itemLength;

			using (var textWriter = new StringWriter())
			{
				serializer.Serialize(textWriter, array);

				StringBuilder textWriterStringBuilder = textWriter.GetStringBuilder();

				totalLength = textWriterStringBuilder.Length;

				textWriterStringBuilder.Clear();

				serializer.Serialize(textWriter, array.Take(1));

				itemLength = textWriterStringBuilder.Length;
			}

			int ratio = (int)Math.Abs(Math.Floor((double)_maxJsonLength / itemLength));

			if (totalLength <= _maxJsonLength || ratio >= array.Count)
			{
				serializer.Serialize(writer, value);
				return;
			}

			if (itemLength >= _maxJsonLength)
			{
				serializer.Serialize(writer, array.Take(1));
				return;
			}

			serializer.Serialize(writer, array.Take(ratio));
		}

		public override IEnumerable<T> ReadJson(JsonReader reader, Type objectType, IEnumerable<T> existingValue, bool hasExistingValue,
			JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}
	}
}
