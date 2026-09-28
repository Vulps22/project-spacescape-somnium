namespace SomniumSpace.Worlds.SpaceScape.Player
{
    /// Who a player is, in our own words: an id and a display name. A snapshot, not a handle on
    /// Somnium's live player object.
    public readonly struct PlayerIdentity
    {
        /// Nobody: what a lookup returns for an id this client has never seen.
        public static readonly PlayerIdentity None = default;

        public string Id { get; }
        public string Name { get; }

        /// Ask this rather than comparing with null: a struct is never null.
        public bool Exists => !string.IsNullOrEmpty(Id);

        public PlayerIdentity(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString() => Exists ? $"'{Name}' ({Id})" : "nobody";
    }
}
