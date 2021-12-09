using System.Runtime.Serialization;

namespace Altinn.Notifications.Integrations
{
    /// <summary>
    /// Represents an exception for when a typed HttpClient recieved an unexpected reponse code and
    /// needs a way to terminate its execution.
    /// </summary>
    [Serializable]
    public sealed class UnhandledHttpResponseException : Exception
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
        /// Initialize a new instance of the <see cref="UnhandledHttpResponseException"/> class with default values.
        /// </summary>
        public UnhandledHttpResponseException()
        {
        }

        /// <summary>
        /// Initialize a new instance of the <see cref="UnhandledHttpResponseException"/> class with the 
        /// given message and custom property values.
        /// </summary>
        /// <remarks>
        /// This is used by the <see cref="GetObjectData(SerializationInfo, StreamingContext)"/> method.
        /// </remarks>
        /// <param name="statusCode">The response status code of the unhandled response.</param>
        /// <param name="reasonPhrase">The reason phrase of the unhandled response.</param>
        /// <param name="content">The content of the unhandled response.</param>
        private UnhandledHttpResponseException(int statusCode, string reasonPhrase, string content)
            : base(FormatMessage(statusCode, reasonPhrase))
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
            Content = content;
        }

        /// <summary>
        /// Initialize a new instance of the <see cref="UnhandledHttpResponseException"/> class using the
        /// given <see cref="SerializationInfo"/>.
        /// </summary>
        private UnhandledHttpResponseException(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            StatusCode = (int)info.GetValue(nameof(StatusCode), typeof(int))!;
            ReasonPhrase = (string)info.GetValue(nameof(ReasonPhrase), typeof(string))!;
            // Property Content excluded on purpose. It might contain data we don't want stored in logs.
        }

        /// <summary>
        /// Create a new <see cref="UnhandledHttpResponseException"/> by reading the <see cref="HttpResponseMessage"/>
        /// content asynchronously.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/> to read.</param>
        /// <returns>A new <see cref="UnhandledHttpResponseException"/>.</returns>
        public static async Task<UnhandledHttpResponseException> CreateAsync(HttpResponseMessage response)
        {
            string content = await response.Content.ReadAsStringAsync();
            return new UnhandledHttpResponseException((int)response.StatusCode, response.ReasonPhrase!, content);
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
            // Property Content excluded on purpose. It might contain data we don't want stored in logs.
            base.GetObjectData(info, context);
        }

        private static string FormatMessage(int statusCode, string reasonPhrase)
        {
            return $"{statusCode} - {reasonPhrase}";
        }
    }
}
