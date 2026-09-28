using System;
using System.Collections.Generic;

namespace SomniumSpace.Worlds.SpaceScape.Power
{
    /// The ship's grid. Power splits at every fork by the conduits' shares, components are endpoints,
    /// and wasted power becomes heat that travels back along the run.
    public sealed class PowerGraph
    {
        /// Everything sits at this temperature when nothing is wasting power.
        public const double AmbientCelsius = 21.0;

        /// Degrees a tile gains per watt it wastes, per second.
        public double DegreesPerWastedWattSecond = 0.05;

        /// Degrees a tile sheds per second while it is not wasting anything.
        public double CoolingDegreesPerSecond = 0.2;

        /// Share of the gap between two tiles that travels along the conduit between them, per second.
        public double ConductionPerSecond = 4.0;

        /// How long a 100 W tile sitting at 120 C lasts, on average, before it blows. This is the dial
        /// for how fragile the grid is; the hazard constant is worked out from it.
        public double PopSecondsAt100WAnd120C = 30.0;

        /// Below this a tile never blows, however much it is carrying.
        public double PopFloorCelsius = 50.0;

        /// And it is never blamed for being less than this far over where it should be, so a reactor
        /// sitting at its own working temperature is never damaged for it.
        public double PopFloorAboveBaseline = 1.0;

        /// Condition a component loses each time heat gets the better of it. Four hits ends one.
        public double PopDamage = 25.0;

        /// Seeded so a run can be repeated.
        public int PopSeed = 20260917;

        /// True where this grid decides its own failures: heat pops, the damage they do, and rings blowing.
        /// False on a networked client that is told what failed by the master instead of rolling for it.
        public bool DecidesFailures = true;

        private readonly List<PowerNode> _nodes = new List<PowerNode>();
        private readonly List<PowerEdge> _edges = new List<PowerEdge>();
        private double[] _heatDelta;
        private Stack<PowerNode> _reachStack;
        private Random _rng;
        private List<PowerNode> _mergePoints;

        /// Raised for a bare conduit the instant it blows.
        public event Action<PowerNode> Popped;

        /// Raised for a component each time a thermal failure damages it.
        public event Action<PowerNode> Damaged;

        /// Raised for a component the moment its condition reaches zero.
        public event Action<PowerNode> Lost;

        public IReadOnlyList<PowerNode> Nodes => _nodes;
        public IReadOnlyList<PowerEdge> Edges => _edges;

        /// Adds a junction, or a module that draws power.
        public PowerNode AddNode(string name, IPowerSink sink = null)
        {
            var node = new PowerNode(name) { Sink = sink, Index = _nodes.Count };
            _nodes.Add(node);
            return node;
        }

        /// Adds a node that puts power onto the grid, and may draw from it too.
        public PowerNode AddSource(string name, IPowerSource source, IPowerSink sink = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var node = new PowerNode(name) { Source = source, Sink = sink, Index = _nodes.Count };
            _nodes.Add(node);
            return node;
        }

        /// Adds a node whose output never varies.
        public PowerNode AddSource(string name, double watts) => AddSource(name, new ConstantSourceBehaviour(watts));

        /// Runs a conduit from one node to another.
        public PowerEdge Connect(PowerNode from, PowerNode to)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            if (ReferenceEquals(from, to)) throw new ArgumentException($"conduit from '{from.Name}' to itself");

            var edge = new PowerEdge(from, to);
            from.Outgoing.Add(edge);
            to.Incoming.Add(edge);
            _edges.Add(edge);
            return edge;
        }

        /// Mends a tile that has blown, so it carries power again. It keeps the heat it blew with.
        public void Repair(PowerNode node)
        {
            if (node == null) return;
            node.IsPopped = false;
        }

        /// Takes a conduit out of the grid. Whatever it was carrying this tick goes with it.
        public void Disconnect(PowerEdge edge)
        {
            if (edge == null) return;
            edge.From.Outgoing.Remove(edge);
            edge.To.Incoming.Remove(edge);
            _edges.Remove(edge);
        }

        /// Advances the grid one tick. Every node reads the last tick's conduits, so order does not matter.
        /// Power therefore moves one conduit per tick, and a node's readings sit one step behind the
        /// conduits leaving it until the grid settles. Settle() runs it out to the fixed point.
        public void Tick(double seconds)
        {
            MarkReachability();
            FindMergePoints();

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];

                bool hasOutlet = false;
                for (int j = 0; j < node.Outgoing.Count; j++)
                {
                    var out_ = node.Outgoing[j];
                    if (Carries(out_)) { hasOutlet = true; break; }
                }
                node.HasOutlet = hasOutlet;
                node.Offered = node.Source != null && !node.IsWrecked
                    ? node.Source.WattsOffered(node, seconds)
                    : 0.0;

                double arriving = 0.0;
                for (int j = 0; j < node.Incoming.Count; j++)
                {
                    var edge = node.Incoming[j];
                    if (edge.Enabled) arriving += edge.Flow;
                }
                if (!node.CanReceivePower())
                {
                    node.HasOutlet = false;
                    node.Offered = 0.0;
                    node.Arriving = 0.0;
                    node.Inflow = 0.0;
                    continue;
                }
                node.Arriving = arriving;
                node.Inflow = arriving + node.Offered;
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];

                // A sink draws only from what arrived, never from this node's own output: a store
                // charges from its input conduit and discharges down its output, not into itself.
                double wanted = node.Sink != null && !node.IsWrecked ? node.Sink.WattsWanted : 0.0;
                if (wanted < 0.0) wanted = 0.0;
                double drawn = wanted < node.Arriving ? wanted : node.Arriving;

                // A conduit carries on whatever arrived. A component does not: its output offers only
                // what it chose to release, and whatever arrived that it did not take has nowhere to go.
                double blocked = 0.0;
                double remainder;
                if (node.ForwardsPower)
                {
                    remainder = node.Inflow - drawn;
                }
                else
                {
                    blocked = node.Arriving - drawn;
                    if (blocked < 0.0) blocked = 0.0;
                    remainder = node.Offered;
                }

                if (!node.CanReceivePower())
                {
                    node.Drawn = 0.0;
                    node.Passed = 0.0;
                    node.Dumped = 0.0;
                    for (int j = 0; j < node.Outgoing.Count; j++) node.Outgoing[j].NextFlow = 0.0;
                    continue;
                }

                double shares = 0.0;
                for (int j = 0; j < node.Outgoing.Count; j++)
                {
                    var edge = node.Outgoing[j];
                    if (Carries(edge)) shares += edge.Share;
                }

                node.Drawn = drawn;

                if (shares > 0.0)
                {
                    for (int j = 0; j < node.Outgoing.Count; j++)
                    {
                        var edge = node.Outgoing[j];
                        edge.NextFlow = Carries(edge)
                            ? remainder * (edge.Share / shares)
                            : 0.0;
                    }
                    node.Passed = remainder;
                    node.Dumped = blocked;
                }
                else
                {
                    for (int j = 0; j < node.Outgoing.Count; j++) node.Outgoing[j].NextFlow = 0.0;
                    node.Passed = 0.0;
                    node.Dumped = remainder + blocked;
                }
            }

            for (int i = 0; i < _edges.Count; i++) _edges[i].Flow = _edges[i].NextFlow;

            // Wasting power heats a tile; anything not wasting sheds back down to ambient. Rates are
            // per second rather than per tick so the tick rate never changes how the ship behaves.
            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node.Dumped > 0.0)
                {
                    node.Celsius += node.Dumped * DegreesPerWastedWattSecond * seconds;
                    continue;
                }
                double floor = node.BaselineCelsius;
                if (node.Celsius <= floor) { node.Celsius = floor; continue; }

                double cooled = node.Celsius - CoolingDegreesPerSecond * seconds;
                node.Celsius = cooled < floor ? floor : cooled;
            }

            Conduct(seconds);
            if (DecidesFailures)
            {
                RollForPops(seconds);
                BlowMergePoints();
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                // A tile that is off still tells its sink it got nothing, so a lamp coasts down on
                // what it holds rather than freezing. Its source is left alone, so a cell keeps its charge.
                if (!node.CanReceivePower())
                {
                    if (node.Sink != null) node.Sink.Receive(0.0, seconds);
                    continue;
                }
                if (node.Source != null) node.Source.ProvidePower(node, seconds);
                if (node.Sink != null) node.Sink.Receive(node.Drawn, seconds);
            }
        }

        /// Walks backwards from everything that can draw power, so a cell can tell the difference
        /// between having a cable, having a cable that reaches a component, and having one that
        /// reaches a component asking for something. One pass, not one per source.
        private void MarkReachability()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].ReachesConsumer = false;
                _nodes[i].ReachesDemand = false;
            }

            if (_reachStack == null) _reachStack = new Stack<PowerNode>();

            // Every receiver marks whatever feeds it, not itself: a battery's own intake is not somewhere
            // its output can go. A wrecked receiver still counts, since it stays wired in and keeps taking.
            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!IsReceiver(node)) continue;
                bool demanding = !node.IsWrecked && node.Sink.WattsWanted > 0.0;
                for (int j = 0; j < node.Incoming.Count; j++)
                {
                    var edge = node.Incoming[j];
                    if (!edge.Enabled || edge.Share <= 0.0 || !edge.From.CanReceivePower()) continue;
                    Flood(edge.From, demanding);
                }
            }
        }

        /// A component that takes power in, whether or not it wants any right now.
        private static bool IsReceiver(PowerNode node) => node.Sink != null && node.CanReceivePower();

        /// True when a conduit can carry power this tick: it is live, and there is a receiver at its far end
        /// or somewhere beyond it through conduits. Nothing is pushed down a branch with nobody on it.
        private static bool Carries(PowerEdge edge) =>
            edge.Enabled && edge.Share > 0.0 && edge.To.CanReceivePower()
            && (IsReceiver(edge.To) || edge.To.ReachesConsumer);

        /// Marks this tile and everything that can feed it, without walking the same ground twice.
        private void Flood(PowerNode from, bool demanding)
        {
            _reachStack.Clear();
            _reachStack.Push(from);

            while (_reachStack.Count > 0)
            {
                var node = _reachStack.Pop();

                bool already = node.ReachesConsumer && (node.ReachesDemand || !demanding);
                node.ReachesConsumer = true;
                if (demanding) node.ReachesDemand = true;
                if (already) continue;

                // Power does not pass through a component, so neither does reaching a receiver.
                if (!node.ForwardsPower) continue;

                for (int i = 0; i < node.Incoming.Count; i++)
                {
                    var edge = node.Incoming[i];
                    if (!edge.Enabled || edge.Share <= 0.0) continue;
                    if (!edge.From.CanReceivePower()) continue;
                    _reachStack.Push(edge.From);
                }
            }
        }

        /// Gives every hot tile its roll. Power times heat is the hazard, so the tile that goes need
        /// not be the hottest one.
        private void RollForPops(double seconds)
        {
            if (PopSecondsAt100WAnd120C <= 0.0) return;
            if (_rng == null) _rng = new Random(PopSeed);

            double k = 1.0 / (100.0 * (120.0 - AmbientCelsius) * PopSecondsAt100WAnd120C);

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node.IsPopped) continue;
                if (node.Celsius < PopFloorCelsius) continue;
                if (node.Celsius < node.BaselineCelsius + PopFloorAboveBaseline) continue;

                double power = node.Inflow;
                double heat = node.HeatAboveBaseline;
                if (power <= 0.0 || heat <= 0.0) continue;

                double chance = 1.0 - Math.Exp(-k * power * heat * seconds);
                if (_rng.NextDouble() >= chance) continue;

                // A bare conduit severs. Anything with integrity takes it on its condition instead; a
                // component stays wired in, so it keeps being fed, keeps wasting, and keeps wearing down.
                if (node.Integrity == null)
                {
                    Sever(node);
                    continue;
                }

                if (node.Integrity.IsDestroyed) continue;
                node.Integrity.TakeDamage(PopDamage);
                Damaged?.Invoke(node);
                if (!node.Integrity.IsDestroyed) continue;
                Lost?.Invoke(node);
                if (node.ForwardsPower) Sever(node);
            }
        }

        /// Sets whether a tile is severed, for the network to bring a copy into line with the master's.
        public void CorrectPopped(PowerNode node, bool popped)
        {
            if (node == null || node.IsPopped == popped) return;
            if (popped) Sever(node);
            else node.IsPopped = false;
        }

        /// Cuts a tile out of the grid for good.
        private void Sever(PowerNode node)
        {
            // What was already on its way out of it stops here too.
            for (int i = 0; i < node.Outgoing.Count; i++)
            {
                node.Outgoing[i].Flow = 0.0;
                node.Outgoing[i].NextFlow = 0.0;
            }
            node.IsPopped = true;
            Popped?.Invoke(node);
        }

        /// Collects every tile where power enters a ring, from the conduits live this tick.
        private void FindMergePoints()
        {
            if (_mergePoints == null) _mergePoints = new List<PowerNode>();
            _mergePoints.Clear();

            foreach (var ring in FindRings(live: true))
            {
                var members = new HashSet<PowerNode>(ring);
                foreach (var node in ring)
                {
                    foreach (var edge in node.Incoming)
                    {
                        if (!IsLive(edge) || members.Contains(edge.From)) continue;
                        _mergePoints.Add(node);
                        break;
                    }
                }
            }
        }

        /// Blows every merge point power has reached. A ring cannot be fed without shorting.
        private void BlowMergePoints()
        {
            for (int i = 0; i < _mergePoints.Count; i++)
            {
                var node = _mergePoints[i];
                if (node.IsPopped || node.Arriving <= 0.0) continue;

                if (node.Integrity != null && !node.Integrity.IsDestroyed)
                {
                    node.Integrity.TakeDamage(node.Integrity.Current);
                    Damaged?.Invoke(node);
                    Lost?.Invoke(node);
                }
                Sever(node);
            }
        }

        /// True when a conduit can carry power this tick.
        private static bool IsLive(PowerEdge edge) =>
            edge.Enabled && edge.Share > 0.0 && edge.From.CanReceivePower() && edge.To.CanReceivePower();

        /// Groups of forwarding tiles that power can travel round and round, found as strongly
        /// connected components of the conduits. Components never forward, so they never join one.
        private List<List<PowerNode>> FindRings(bool live)
        {
            var rings = new List<List<PowerNode>>();
            var index = new Dictionary<PowerNode, int>();
            var low = new Dictionary<PowerNode, int>();
            var onStack = new HashSet<PowerNode>();
            var stack = new Stack<PowerNode>();
            int counter = 0;

            foreach (var n in _nodes)
                if (InRingGraph(n, live) && !index.ContainsKey(n))
                    Visit(n);

            return rings;

            void Visit(PowerNode node)
            {
                index[node] = low[node] = counter++;
                stack.Push(node);
                onStack.Add(node);

                foreach (var edge in node.Outgoing)
                {
                    var next = edge.To;
                    if (!InRingGraph(next, live) || (live && !IsLive(edge))) continue;
                    if (!index.ContainsKey(next))
                    {
                        Visit(next);
                        if (low[next] < low[node]) low[node] = low[next];
                    }
                    else if (onStack.Contains(next) && index[next] < low[node])
                    {
                        low[node] = index[next];
                    }
                }

                if (low[node] != index[node]) return;

                var group = new List<PowerNode>();
                PowerNode popped;
                do
                {
                    popped = stack.Pop();
                    onStack.Remove(popped);
                    group.Add(popped);
                } while (popped != node);

                if (group.Count > 1) rings.Add(group);
            }
        }

        /// Whether a tile counts when looking for rings: only conduits, and only live ones when asked.
        private static bool InRingGraph(PowerNode node, bool live) =>
            node.ForwardsPower && (!live || node.CanReceivePower());

        /// Moves excess heat along the conduits, from the tile furthest over where it should be to
        /// the one least over. Computed against the tick's starting temperatures and applied
        /// afterwards, so no tile's order matters, and what one loses is exactly what the next gains.
        private void Conduct(double seconds)
        {
            if (_edges.Count == 0) return;

            if (_heatDelta == null || _heatDelta.Length < _nodes.Count) _heatDelta = new double[_nodes.Count];
            Array.Clear(_heatDelta, 0, _nodes.Count);

            double share = ConductionPerSecond * seconds;
            if (share > 0.5) share = 0.5;      // beyond half the gap per tick it would overshoot and ring
            if (share <= 0.0) return;

            for (int i = 0; i < _edges.Count; i++)
            {
                // A cable carries heat whether or not it is carrying power: an open switch stops the
                // current, not the metal.
                var edge = _edges[i];
                if (edge.From.IsPopped || edge.To.IsPopped) continue;   // severed metal carries nothing

                // Only heat a tile was never meant to have travels. A reactor at its working
                // temperature is not hot, it is correct, and correct does not spread - otherwise the
                // baseline would refill every tick and manufacture heat out of nothing.
                double gap = edge.From.HeatAboveBaseline - edge.To.HeatAboveBaseline;
                if (gap == 0.0) continue;

                double moved = gap * share;
                _heatDelta[edge.From.Index] -= moved;
                _heatDelta[edge.To.Index] += moved;
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                double next = node.Celsius + _heatDelta[node.Index];
                node.Celsius = next < node.BaselineCelsius ? node.BaselineCelsius : next;
            }
        }

        /// Ticks until conduit flows stop changing, which is where node and conduit readings agree.
        public int Settle(double seconds = 0.02, int maxTicks = 512, double epsilon = 1e-9)
        {
            for (int t = 1; t <= maxTicks; t++)
            {
                var before = new double[_edges.Count];
                for (int i = 0; i < _edges.Count; i++) before[i] = _edges[i].Flow;

                Tick(seconds);

                double worst = 0.0;
                for (int i = 0; i < _edges.Count; i++)
                {
                    double d = Math.Abs(_edges[i].Flow - before[i]);
                    if (d > worst) worst = d;
                }
                if (worst <= epsilon) return t;
            }
            return maxTicks;
        }

        /// Watts every source on the grid put out on the last tick.
        public double TotalInjected
        {
            get { double s = 0.0; foreach (var n in _nodes) s += n.Offered; return s; }
        }

        /// Watts consumed by modules on the last tick.
        public double TotalDrawn
        {
            get { double s = 0.0; foreach (var n in _nodes) s += n.Drawn; return s; }
        }

        /// Watts that ran out of conduit on the last tick. This is the grid's heat budget.
        public double TotalDumped
        {
            get { double s = 0.0; foreach (var n in _nodes) s += n.Dumped; return s; }
        }

        /// Reports wiring that will misbehave rather than leaving it to be found in flight.
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            bool anySource = false;
            foreach (var n in _nodes) if (n.Source != null) { anySource = true; break; }
            if (!anySource) problems.Add("no node on the grid produces power");

            foreach (var ring in FindRings(live: false))
            {
                var names = new List<string>();
                foreach (var n in ring) names.Add(n.Name);
                problems.Add("ring (its merge point blows the moment power reaches it): " + string.Join(", ", names));
            }

            var fed = ReachableFromSources();
            foreach (var node in _nodes)
            {
                if (node.Sink == null || fed.Contains(node)) continue;
                problems.Add($"'{node.Name}' draws power but no conduit reaches it from a source");
            }

            foreach (var node in _nodes)
            {
                if (node.Incoming.Count == 0 && node.Outgoing.Count == 0 && node.Source == null)
                    problems.Add($"'{node.Name}' is not wired to anything");
            }

            return problems;
        }

        /// Every node a source can push power to, following conduits a switch has not opened.
        private HashSet<PowerNode> ReachableFromSources()
        {
            var seen = new HashSet<PowerNode>();
            var stack = new Stack<PowerNode>();
            foreach (var n in _nodes)
                if (n.Source != null && seen.Add(n)) stack.Push(n);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                foreach (var edge in node.LiveOutgoing)
                    if (seen.Add(edge.To)) stack.Push(edge.To);
            }
            return seen;
        }
    }
}
