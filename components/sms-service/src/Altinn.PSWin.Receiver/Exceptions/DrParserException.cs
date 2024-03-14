namespace LinkMobility.PSWin.Receiver.Exceptions
{

    [System.Serializable]
    public class DrParserException : System.Exception
    {
        public DrParserException() { }
        public DrParserException(string message) : base(message) { }
        public DrParserException(string message, System.Exception inner) : base(message, inner) { }
        protected DrParserException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
