namespace Models.Banking
{
    public class Transaction : AmountedEntity<string, string>
    {
        public override string Id { get; set; }
        public override string Source { get; set; }
        public override string Target { get; set; }
        public override double Amount { get; set; }
        public string Timestamp { get; set; }
        public int TrackingId { get; set; }
        public string Type { get; set; }
    }
}