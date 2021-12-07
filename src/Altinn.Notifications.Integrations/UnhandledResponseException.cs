using System.Net;
using System.Runtime.Serialization;

namespace Altinn.App.PlatformServices.Helpers
{
    /// <summary>
    /// Represents an exception for when a typed HttpClient recieved an unexpected reponse code and
    /// needs a way to terminate its execution.
    /// </summary>
    [Serializable]
    public class UnhandledResponseException : Exception
    {
        /// <summary>
        /// The response status code of the unhandled response.
        /// </summary>
        public int StatusCode { get; } = 0;

        /// <summary>
        /// The reason phrase of the unhandled response.
        /// </summary>
        public string ReasonPhrase { get; } = string.Empty;

        /// <summary>
        /// The content of the unhandled response.
        /// </summary>
        public string Content { get; } = string.Empty;

        /// <summary>
        /// Initialize a new instance of the <see cref="UnhandledResponseException"/> class with default values.
        /// </summary>
        public UnhandledResponseException()
        {
        }

        /// <summary>
        /// Initialize a new instance of the <see cref="UnhandledResponseException"/> class with the 
        /// given message and custom property values.
        /// </summary>
        /// <remarks>
        /// This is used by the <see cref="GetObjectData(SerializationInfo, StreamingContext)"/> method.
        /// </remarks>
        /// <param name="statusCode">The response status code of the unhandled response.</param>
        /// <param name="reasonPhrase">The reason phrase of the unhandled response.</param>
        /// <param name="content">The content of the unhandled response.</param>
        private UnhandledResponseException(int statusCode, string reasonPhrase, string content)
            : base(FormatMessage(statusCode, reasonPhrase))
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
            Content = content;
        }

        /// <summary>
        /// Initialize a new instance of the <see cref="UnhandledResponseException"/> class using the
        /// given <see cref="SerializationInfo"/>.
        /// </summary>
        private UnhandledResponseException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            StatusCode = (int)info.GetValue(nameof(StatusCode), typeof(int))!;
            ReasonPhrase = (string)info.GetValue(nameof(ReasonPhrase), typeof(string))!;
            Content = (string)info.GetValue(nameof(Content), typeof(string))!;
        }

        /// <summary>
        /// Create a new <see cref="UnhandledResponseException"/> by reading the <see cref="HttpResponseMessage"/>
        /// content asynchronously.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/> to read.</param>
        /// <returns>A new <see cref="UnhandledResponseException"/>.</returns>
        public static async Task<UnhandledResponseException> CreateAsync(HttpResponseMessage response)
        {
            string content = await response.Content.ReadAsStringAsync();
            return new UnhandledResponseException((int)response.StatusCode, response.ReasonPhrase!, content);
        }

        /// <summary>
        /// Populates a <see cref="SerializationInfo"/> with the data needed to serialize the target object.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="StreamingContext" />) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(StatusCode), StatusCode);
            info.AddValue(nameof(ReasonPhrase), ReasonPhrase);
            info.AddValue(nameof(Content), Content);
            base.GetObjectData(info, context);
        }

        private static string FormatMessage(int statusCode, string reasonPhrase)
        {
            return $"{statusCode} - {reasonPhrase}";
        }
    }
}
