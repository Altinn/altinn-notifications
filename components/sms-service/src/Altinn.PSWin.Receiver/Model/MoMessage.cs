namespace LinkMobility.PSWin.Receiver.Model
{
    public class MoMessage
    {
        public MoMessage(string text, string sender, string receiver, Address address, Position position, Metadata metadata)
        {
            Text = text;
            Sender = sender;
            Receiver = receiver;
            Address = address;
            Position = position;
            Metadata = metadata;
        }
        public string Text { get; }
        public string Receiver { get; }
        public string Sender { get; }
        public Address Address { get; }
        public Position Position { get; }
        public Metadata Metadata { get; }
    }
}
