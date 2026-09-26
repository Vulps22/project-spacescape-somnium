using System;
using System.Collections.Generic;

namespace SpaceScape.Power
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

        /// Seeded so a run can be repeated, and so every client would agree.
        public int PopSeed = 20260917;

        private readonly List<PowerNode> _nodes = new List<PowerNode>();
        private readonly List<PowerEdge> _edges = new List<PowerEdge>();
        private double[] _heatDelta;
        private Stack<PowerNode> _reachStack;
        private Random _rng;

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
        public PowerNode AddSource(string name, double watts) => AddSource(name, new ConstantSource(watts));

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

        /// Advances the grid one tick. Every node reads the last tick's conduits, so order does not matter.
        /// Power therefore moves one conduit per tick, and a node's readings sit one step behind the
        /// conduits leaving it until the grid settles. Settle() runs it out to the fixed point.
        public void Tick(double seconds)
        {
            MarkReachability();

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];

                bool hasOutlet = false;
                for (int j = 0; j < node.Outgoing.Count; j++)
                {
                    var out_ = node.Outgoing[j];
                    if (out_.Enabled && out_.Share > 0.0 && out_.To.CanReceivePower()) { hasOutlet = true; break; }
                }
                node.HasOutlet = hasOutlet;
                node.Offered = node.Source != null && !node.IsWrecked
                    ? node.Source.WattsOffered(node)
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

                // Without a diode, whatever arrived and was not used carries on downstream. With
                // one, it stops here: the output offers only what this node produced, and the
                // surplus that arrived has nowhere left to go.
                double blocked = 0.0;
                double remainder;
                if (node.PassesThrough)
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
                    if (edge.Enabled && edge.Share > 0.0 && edge.To.CanReceivePower()) shares += edge.Share;
                }

                node.Drawn = drawn;

                if (shares > 0.0)
                {
                    for (int j = 0; j < node.Outgoing.Count; j++)
                    {
                        var edge = node.Outgoing[j];
                        edge.NextFlow = edge.Enabled && edge.Share > 0.0 && edge.To.CanReceivePower()
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
            RollForPops(seconds);

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

            // anything with a sink is a destination; anything whose sink wants watts is a demand
            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node.Sink == null || node.IsWrecked || !node.CanReceivePower()) continue;
                Flood(node, node.Sink.WattsWanted > 0.0);
            }
        }

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

                // A bare conduit severs. A component takes it on its condition instead and stays
                // wired in, so it keeps being fed, keeps wasting, and keeps wearing itself down.
                if (node.Durability == null)
                {
                    node.IsPopped = true;
                    Popped?.Invoke(node);
                    continue;
                }

                if (node.Durability.IsDestroyed) continue;
                node.Durability.TakeDamage(PopDamage);
                Damaged?.Invoke(node);
                if (node.Durability.IsDestroyed) Lost?.Invoke(node);
            }
        }

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

            foreach (var cycle in FindCycles())
                problems.Add("cycle (power never leaves it, so the grid will not conserve): " + string.Join(" -> ", cycle));

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

        /// Finds loops in the wiring, which trap power and break conservation.
        private List<List<string>> FindCycles()
        {
            var found = new List<List<string>>();
            var state = new Dictionary<PowerNode, int>();   // 0 unseen, 1 on the stack, 2 done
            var path = new List<PowerNode>();

            foreach (var n in _nodes) state[n] = 0;
            foreach (var n in _nodes)
                if (state[n] == 0) Walk(n, state, path, found);

            return found;
        }

        /// Depth-first walk that records a loop the moment it steps onto its own path.
        private void Walk(PowerNode node, Dictionary<PowerNode, int> state, List<PowerNode> path, List<List<string>> found)
        {
            state[node] = 1;
            path.Add(node);

            foreach (var edge in node.Outgoing)
            {
                var next = edge.To;
                if (state[next] == 1)
                {
                    var names = new List<string>();
                    int start = path.IndexOf(next);
                    for (int i = start; i < path.Count; i++) names.Add(path[i].Name);
                    names.Add(next.Name);
                    found.Add(names);
                }
                else if (state[next] == 0)
                {
                    Walk(next, state, path, found);
                }
            }

            path.RemoveAt(path.Count - 1);
            state[node] = 2;
        }
    }
}
