namespace LinkMobility.PSWin.Receiver.Exceptions
{
    [System.Serializable]
    public class MoParserException : System.Exception
    {
        public MoParserException() { }
        public MoParserException(string message) : base(message) { }
        public MoParserException(string message, System.Exception inner) : base(message, inner) { }
        protected MoParserException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
