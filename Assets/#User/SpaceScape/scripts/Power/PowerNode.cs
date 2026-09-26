using System.Collections.Generic;

namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// A point on the grid. Sums what arrives, takes what it wants, splits the rest evenly downstream.
    public class PowerNode
    {
        public readonly string Name;

        /// What puts power onto the grid here. Null means the node produces nothing.
        public IPowerSource Source;

        /// What draws power here. Null means the node only routes.
        public IPowerSink Sink;

        /// False when whatever is on this tile is switched off.
        public bool On = true;

        /// Whether power arriving here may leave again by an output. Only a bare conduit forwards: a
        /// component's input fills its own hold and its output carries only what it chose to release.
        public bool ForwardsPower => Source == null && Sink == null;

        /// True once this tile has blown. Unlike a switch it cannot be undone by flipping anything.
        /// Only tiles without integrity blow: anything with integrity takes damage instead.
        public bool IsPopped { get; internal set; }

        /// Set when a component sits here. A thermal failure damages it rather than severing the
        /// tile. A wrecked component stays wired in and keeps taking what it is given - it simply
        /// stops doing anything with it, so it becomes a dead end that cooks its own compartment.
        public Integrity Integrity;

        /// True once the component here is beyond saving. It stays wired in and keeps taking what it
        /// is given; it simply does nothing with it.
        public bool IsWrecked => Integrity != null && Integrity.IsDestroyed;

        /// Whether the grid should process this tile at all. Asked every tick rather than cached, and
        /// overridable so a breaker or a fault can answer with more than a flag.
        public virtual bool CanReceivePower() => On && !IsPopped;

        /// Where this node sits in the graph's list, so heat can be shuffled without a lookup.
        internal int Index;

        internal readonly List<PowerEdge> Incoming = new List<PowerEdge>();
        internal readonly List<PowerEdge> Outgoing = new List<PowerEdge>();

        /// Conduits arriving here.
        public IReadOnlyList<PowerEdge> IncomingEdges => Incoming;

        /// Conduits leaving here.
        public IReadOnlyList<PowerEdge> OutgoingEdges => Outgoing;

        /// Watts that arrived down incoming conduits this tick. A sink here may draw only from this.
        public double Arriving { get; internal set; }

        /// Everything available at this node: what arrived plus whatever a source here offered.
        public double Inflow { get; internal set; }

        /// Watts the sink here took.
        public double Drawn { get; internal set; }

        /// Watts handed to downstream conduits.
        public double Passed { get; internal set; }

        /// Watts that reached this node with nowhere left to go. This is what becomes heat.
        public double Dumped { get; internal set; }

        /// How hot this tile is, in degrees. Wasting power raises it; a tile that is not wasting and
        /// is above where it should be sheds back down to that.
        public double Celsius { get; internal set; } = PowerGraph.AmbientCelsius;

        /// How hot this tile is *meant* to be. Ambient for everything that is not making heat on
        /// purpose; a running reactor raises its own, because its heat ratio is a baseline and not a
        /// fault. Nothing sheds below this, and nothing is damaged for merely sitting at it.
        public double BaselineCelsius = PowerGraph.AmbientCelsius;

        /// Degrees hotter than this tile should be, which is what damage is built from. A reactor at
        /// its working temperature reads zero here; the same reactor fifty degrees over does not.
        public double HeatAboveBaseline => Celsius > BaselineCelsius ? Celsius - BaselineCelsius : 0.0;

        protected internal PowerNode(string name)
        {
            Name = name;
        }

        /// Watts the source here offered this tick, decided by the source when the grid asked.
        public double Offered { get; internal set; }

        /// True when at least one conduit leaving here can carry power.
        public bool HasOutlet { get; internal set; }

        /// True when some component is reachable from here at all, whether or not it wants anything
        /// right now. A run that ends in bare cable is false, which is what a safety cell checks.
        public bool ReachesConsumer { get; internal set; }

        /// True when something reachable from here is actually asking for watts this tick.
        public bool ReachesDemand { get; internal set; }

        /// Conduits leaving here that a switch has not opened.
        public IEnumerable<PowerEdge> LiveOutgoing
        {
            get
            {
                foreach (var e in Outgoing)
                    if (e.Enabled) yield return e;
            }
        }

        public override string ToString() =>
            $"{Name}: in {Inflow:0.##} W, drew {Drawn:0.##} W, passed {Passed:0.##} W, dumped {Dumped:0.##} W";
    }
}
