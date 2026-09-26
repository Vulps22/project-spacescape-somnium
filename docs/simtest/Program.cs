using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SpaceScape.Power;

/// A producer that winds its output up at a fixed rate, to prove generation can be stateful.
sealed class RampProducer : IPowerProducer
{
    private readonly double _perSecond;
    public double WattsProduced { get; private set; }
    public double TotalJoules { get; private set; }

    public RampProducer(double startWatts, double wattsPerSecond)
    {
        WattsProduced = startWatts;
        _perSecond = wattsPerSecond;
    }

    public void ProducePower(double seconds)
    {
        TotalJoules += WattsProduced * seconds;
        WattsProduced += _perSecond * seconds;
    }
}

static class Program
{
    static int _pass = 0, _fail = 0;

    static void Check(string what, double actual, double expected, double tol = 1e-6)
    {
        bool ok = Math.Abs(actual - expected) <= tol;
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}: got {actual:0.####}, expected {expected:0.####}");
        if (ok) _pass++; else _fail++;
    }

    static void CheckTrue(string what, bool ok)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
        if (ok) _pass++; else _fail++;
    }

    static void Main()
    {
        EvenSplit();
        MergeSums();
        EndpointsDoNotPassPower();
        PriorityIsBranchDepth();
        MoreCablesPromote();
        ShortStealsFromItsSiblings();
        FaultStaysOnSharedWiring();
        DeadEndStealsPower();
        SwitchTrap();
        Conservation();
        DutyCycleIsThePowerFraction();
        NothingStartsInstantly();
        NothingStopsInstantly();
        ZeroDrainHolds();
        IdleShipDumps();
        CycleDetected();
        RingBlowsItsMergePoint();
        UnfedRingStaysQuiet();
        ComponentsBreakRings();
        OpenSwitchBreaksARing();
        RingTakesAllOfAMergePointsIntegrity();
        SurgePropagation();
        VaryingSource();
        BatteryIsBoth();
        ProducerCannotDecline();
        StoreHoldsWhenIsolated();
        StoreChargesAndSuppliesAtOnce();
        EmptyStoreCannotSupplyMoreThanItGets();
        ThreeTiersOfCell();
        ReactorRunsHotWithoutBeingBroken();
        RodsDropFastAndRiseSlow();
        CoolantCostsSupplies();
        LosingControlPowerDropsTheRods();
        DiodeStopsControlPowerLeakingOut();
        DiodeStopsTheColdStartLoopRunningAway();
        RunningReactorHoldsItsOwnRods();
        RodsCanBeLoweredPartWay();
        ThrottlingBelowHouseLoadSelfScrams();
        SwitchedOffNodeRejectsPower();
        HeatTravelsBackUpTheRun();
        HeatShedsWhenNothingIsWasted();
        PopTimingMatchesTheDial();
        CoolTilesNeverPop();
        PoppedTileStopsEverything();
        LosingASiblingKillsTheSource();
        ComponentsTakeDamageNotPops();
        WornComponentsMisfire();

        Console.WriteLine();
        Console.WriteLine($"=== {_pass} passed, {_fail} failed ===");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }

    // 1kW into two conduits is 500 each; into three is 333 each.
    static void EvenSplit()
    {
        Console.WriteLine("even split at a fork");
        foreach (int branches in new[] { 2, 3, 4 })
        {
            var g = new PowerGraph();
            var r = g.AddSource("source", 1000);
            var ends = new List<PowerNode>();
            for (int i = 0; i < branches; i++)
            {
                var e = g.AddNode("end" + i);
                g.Connect(r, e);
                ends.Add(e);
            }
            g.Settle();
            Check($"{branches} branches each carry", ends[0].Inflow, 1000.0 / branches);
        }
    }

    // Two thirds of a reactor reaches a module by running two conduits to it.
    static void MergeSums()
    {
        Console.WriteLine("merge sums, so two cables carry two thirds");
        var g = new PowerGraph();
        var r = g.AddSource("source", 1000);
        var spare = g.AddNode("spare");
        var m = g.AddNode("merge");
        g.Connect(r, spare);
        g.Connect(r, m);
        g.Connect(r, m);          // second cable to the same place
        g.Settle();
        Check("merge node receives", m.Inflow, 666.6666667, 1e-4);
        Check("spare branch receives", spare.Inflow, 333.3333333, 1e-4);
    }

    // Components are endpoints: nothing flows through one, so surplus stops dead at it.
    static void EndpointsDoNotPassPower()
    {
        Console.WriteLine("a component is a leaf, not a pass-through");
        var bulb = new LoadBehaviour(50, 1e9);

        var g = new PowerGraph();
        var src = g.AddSource("source", 100);
        var bp = g.AddNode("branch point");
        var b = g.AddNode("bulb", bulb);
        g.Connect(src, bp);
        g.Connect(bp, b);
        g.Settle();

        Check("branch point passes everything on", bp.Passed, 100);
        Check("branch point wastes nothing", bp.Dumped, 0);
        Check("bulb takes what it is rated for", b.Drawn, 50);
        Check("the surplus stops at the bulb as heat", b.Dumped, 50);
        Check("nothing travels beyond it", b.Passed, 0);
    }

    // Priority is how many branch points you sit behind, which is what routing expresses.
    static void PriorityIsBranchDepth()
    {
        Console.WriteLine("priority is branch depth, not proximity");
        //    B1
        // R--|------|---B2
        //           \--B3
        var near = new LoadBehaviour(1000, 1e9);
        var far1 = new LoadBehaviour(1000, 1e9);
        var far2 = new LoadBehaviour(1000, 1e9);

        var g = new PowerGraph();
        var src = g.AddSource("source", 1000);
        var bp1 = g.AddNode("branch 1");
        var bp2 = g.AddNode("branch 2");
        var b1 = g.AddNode("B1", near);
        var b2 = g.AddNode("B2", far1);
        var b3 = g.AddNode("B3", far2);

        g.Connect(src, bp1);
        g.Connect(bp1, b1);          // one branch point away
        g.Connect(bp1, bp2);
        g.Connect(bp2, b2);          // two branch points away
        g.Connect(bp2, b3);
        g.Settle();

        Check("one branch out gets half", b1.Inflow, 500);
        Check("two branches out gets a quarter", b2.Inflow, 250);
        Check("and so does its sibling", b3.Inflow, 250);
        Check("nothing is lost on the way", b1.Inflow + b2.Inflow + b3.Inflow, 1000);
    }

    // Two conduits to the same endpoint promote it, which is how a ratio is expressed physically.
    static void MoreCablesPromote()
    {
        Console.WriteLine("more conduits to one place raise its share");
        var hungry = new LoadBehaviour(1000, 1e9);
        var other = new LoadBehaviour(1000, 1e9);

        var g = new PowerGraph();
        var src = g.AddSource("source", 900);
        var bp = g.AddNode("branch point");
        var gun = g.AddNode("gun", hungry);
        var scanner = g.AddNode("scanner", other);
        g.Connect(src, bp);
        g.Connect(bp, gun);
        g.Connect(bp, gun);          // a second cable to the gun
        g.Connect(bp, scanner);
        g.Settle();

        Check("gun gets two thirds", gun.Inflow, 600);
        Check("scanner gets one third", scanner.Inflow, 300);
    }

    // A short behaves as several cables, which starves whatever shares its branch point.
    static void ShortStealsFromItsSiblings()
    {
        Console.WriteLine("a shorted conduit steals its siblings' share");
        var scanner = new LoadBehaviour(1000, 1e9);
        var gun = new LoadBehaviour(1000, 1e9);

        var g = new PowerGraph();
        var src = g.AddSource("source", 1000);
        var bp = g.AddNode("branch point");
        var s = g.AddNode("scanner", scanner);
        var u = g.AddNode("gun", gun);
        var toScanner = g.Connect(bp, s);
        g.Connect(bp, u);
        g.Connect(src, bp);
        g.Settle();

        Check("sound grid: gun has half", u.Inflow, 500);
        Check("sound grid: scanner has half", s.Inflow, 500);

        toScanner.Share = 3.0;                       // the scanner shorts
        g.Settle();

        Check("shorted scanner takes three quarters", s.Inflow, 750);
        Check("the gun browns out to a quarter", u.Inflow, 250);
        CheckTrue("and by eye there is still just one cable to each", g.Edges.Count == 3);
    }

    // A fault only hurts what shares wiring with it, so "what else is on this branch?" is the question.
    static void FaultStaysOnSharedWiring()
    {
        Console.WriteLine("a fault only reaches what shares its branch");
        var near = new LoadBehaviour(1000, 1e9);
        var farA = new LoadBehaviour(1000, 1e9);
        var farB = new LoadBehaviour(1000, 1e9);

        var g = new PowerGraph();
        var src = g.AddSource("source", 1000);
        var top = g.AddNode("top branch");
        var left = g.AddNode("left branch");
        var lampA = g.AddNode("lamp A", farA);
        var lampB = g.AddNode("lamp B", farB);
        var untouched = g.AddNode("other subtree", near);

        g.Connect(src, top);
        g.Connect(top, left);
        g.Connect(top, untouched);      // a different subtree entirely
        var faulty = g.Connect(left, lampA);
        g.Connect(left, lampB);
        g.Settle();

        Check("before: lamp A", lampA.Inflow, 250);
        Check("before: lamp B", lampB.Inflow, 250);
        Check("before: other subtree", untouched.Inflow, 500);

        faulty.Share = 4.0;
        g.Settle();

        Check("after: lamp A hogs its branch", lampA.Inflow, 400);
        Check("after: lamp B starves", lampB.Inflow, 100);
        Check("after: the other subtree is untouched", untouched.Inflow, 500);
    }

    // A conduit to a destroyed room still takes its share at the fork.
    static void DeadEndStealsPower()
    {
        Console.WriteLine("a forgotten dead end steals power");
        var gun = new LoadBehaviour(500, 1e9);

        var g = new PowerGraph();
        var r = g.AddSource("source", 1000);
        var fork = g.AddNode("fork");
        var u = g.AddNode("gun", gun);
        var dead = g.AddNode("wrecked room");
        g.Connect(r, fork); g.Connect(fork, u); g.Connect(fork, dead);
        g.Settle();

        Check("gun only gets half the reactor", u.Inflow, 500);
        Check("the dead end burns the other half", dead.Dumped, 500);
        Check("grid heat equals the wasted half", g.TotalDumped, 500);
    }

    // Switching the module off leaves the branch live; switching at the fork does not.
    static void SwitchTrap()
    {
        Console.WriteLine("the switch trap: at the module vs at the fork");
        var laser = new LoadBehaviour(500, 1e9);

        var g = new PowerGraph();
        var r = g.AddSource("source", 1000);
        var fork = g.AddNode("fork");
        var other = g.AddNode("other branch");
        var l = g.AddNode("laser", laser);
        g.Connect(r, fork);
        var toOther = g.Connect(fork, other);
        var toLaser = g.Connect(fork, l);

        g.Settle();
        Check("laser running, heat at the laser", l.Dumped, 0);

        laser.Enabled = false;            // switch at the module
        g.Settle();
        Check("module off: laser branch still fed", l.Inflow, 500);
        Check("module off: all of it becomes heat", l.Dumped, 500);
        Check("module off: other branch unchanged", other.Inflow, 500);

        laser.Enabled = true;
        toLaser.Enabled = false;          // switch at the fork
        g.Settle();
        Check("fork switch: laser branch dead", l.Inflow, 0);
        Check("fork switch: no heat at the laser", l.Dumped, 0);
        Check("fork switch: other branch now takes it all", other.Inflow, 1000);
        CheckTrue("other branch is the only live one", toOther.Enabled && !toLaser.Enabled);
    }

    // Nothing vanishes: at steady state, injected equals drawn plus dumped.
    static void Conservation()
    {
        Console.WriteLine("conservation at steady state");
        var rng = new Random(12345);
        for (int trial = 0; trial < 200; trial++)
        {
            var g = new PowerGraph();
            var r = g.AddSource("source", 1000);
            var nodes = new List<PowerNode> { r };
            int count = 3 + rng.Next(10);
            for (int i = 0; i < count; i++)
            {
                bool sink = rng.NextDouble() < 0.5;
                var n = g.AddNode("n" + i, sink ? new LoadBehaviour(rng.Next(50, 400), 1e9) : null);
                g.Connect(nodes[rng.Next(nodes.Count)], n);   // always to an existing node, so no cycles
                nodes.Add(n);
            }
            g.Settle();
            double closed = g.TotalDrawn + g.TotalDumped;
            if (Math.Abs(closed - g.TotalInjected) > 1e-6)
            {
                Check($"trial {trial} drawn+dumped", closed, g.TotalInjected);
                return;
            }
        }
        CheckTrue("200 random grids all conserve to 1e-6", true);
    }

    // Duty cycle is the deficit made visible, and energy conservation makes it exact.
    static void DutyCycleIsThePowerFraction()
    {
        Console.WriteLine("an underfed component works for exactly its share of the time");

        double DutyAt(double suppliedWatts)
        {
            var lamp = new LoadBehaviour(100, 100);      // 100 W rated, 100 J threshold
            var g = new PowerGraph();
            var src = g.AddSource("source", suppliedWatts);
            var n = g.AddNode("lamp", lamp);
            g.Connect(src, n);

            const double dt = 0.005;
            int ticks = 0, working = 0;
            for (int i = 0; i < 400000; i++) g.Tick(dt);          // let the cycle establish
            for (int i = 0; i < 400000; i++) { g.Tick(dt); ticks++; if (lamp.Working) working++; }
            return (double)working / ticks;
        }

        Check("100 W of 100 W -> lit all the time", DutyAt(100), 1.00, 0.01);
        Check("50 W of 100 W -> lit half the time", DutyAt(50), 0.50, 0.01);
        Check("25 W of 100 W -> lit a quarter of the time", DutyAt(25), 0.25, 0.01);
        Check("10 W of 100 W -> lit a tenth of the time", DutyAt(10), 0.10, 0.01);
    }

    // Nothing comes on the instant power arrives; it has to reach the threshold first.
    static void NothingStartsInstantly()
    {
        Console.WriteLine("a component has to charge before it works");
        var lamp = new LoadBehaviour(50, 50);           // 50 W rated, 50 J threshold
        var g = new PowerGraph();
        var src = g.AddSource("source", 50);
        var n = g.AddNode("lamp", lamp);
        g.Connect(src, n);

        g.Tick(0.05); g.Tick(0.05);
        CheckTrue("dark on the first ticks", !lamp.Working);

        double t = 0.1;
        while (!lamp.Working && t < 10.0) { g.Tick(0.05); t += 0.05; }
        Check("lights after threshold/supply seconds", t, 1.0, 0.1);
        CheckTrue("and it is working", lamp.Working);
    }

    // Nothing goes off the instant power stops; it coasts down on what it holds.
    static void NothingStopsInstantly()
    {
        Console.WriteLine("a component coasts down when power is cut");
        var lamp = new LoadBehaviour(50, 50);
        var g = new PowerGraph();
        var src = g.AddSource("source", 50);
        var n = g.AddNode("lamp", lamp);
        var cable = g.Connect(src, n);

        for (int i = 0; i < 100; i++) g.Tick(0.05);
        CheckTrue("lit while fed", lamp.Working);

        cable.Enabled = false;                        // pull the conduit
        double t = 0.0;
        while (lamp.Working && t < 10.0) { g.Tick(0.05); t += 0.05; }
        Check("fades over threshold/drain seconds", t, 1.0, 0.1);
        CheckTrue("now dark", !lamp.Working);
    }

    // Zero drain means it banks and holds, which is a gun rather than a lamp.
    static void ZeroDrainHolds()
    {
        Console.WriteLine("a zero-drain accumulator holds its charge");
        var gun = new LoadBehaviour(500, 1000, 0.0);    // draws 500 W, 1000 J, burns nothing
        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };   // not what this test measures
        var src = g.AddSource("source", 500);
        var n = g.AddNode("gun", gun);
        g.Connect(src, n);

        for (int i = 0; i < 200; i++) g.Tick(0.05);
        CheckTrue("charged and ready", gun.Working);
        Check("sitting at capacity", gun.Charge, 1000);
        Check("full so it stops drawing", gun.WattsWanted, 0);
        Check("and the surplus becomes heat", g.TotalDumped, 500);

        gun.Discharge();
        Check("firing empties it", gun.Charge, 0);
        CheckTrue("no longer ready", !gun.Working);
    }

    // A full accumulator stops drawing, so a ship at rest turns its whole reactor into heat.
    static void IdleShipDumps()
    {
        Console.WriteLine("a topped-up ship dumps everything");
        var helm = new LoadBehaviour(200, 100);
        var g = new PowerGraph();
        var r = g.AddSource("source", 500);
        var h = g.AddNode("helm", helm);
        g.Connect(r, h);

        g.Tick(0.02); g.Tick(0.02);
        double busy = g.TotalDumped;

        helm.Enabled = false;                 // stand-in for "nothing left to charge"
        g.Settle();
        double idle = g.TotalDumped;

        CheckTrue($"idle dumps more than busy ({idle:0} W vs {busy:0} W)", idle > busy);
        Check("idle dumps the entire source output", idle, 500);
    }

    // A loop traps power, so the grid says so rather than quietly failing to conserve.
    // A1 -> A2 -> B1 -> B2 -> A1, fed at A1: every tile forwards, so power would circulate forever.
    static void RingBlowsItsMergePoint()
    {
        Console.WriteLine("a ring blows its merge point");
        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var src = g.AddSource("source", 100);
        var a1 = g.AddNode("A1"); var a2 = g.AddNode("A2");
        var b1 = g.AddNode("B1"); var b2 = g.AddNode("B2");
        g.Connect(src, a1); g.Connect(a1, a2); g.Connect(a2, b1); g.Connect(b1, b2); g.Connect(b2, a1);

        var popped = new List<string>();
        g.Popped += n => popped.Add(n.Name);

        g.Tick(0.05);
        CheckTrue("nothing blows before power arrives", popped.Count == 0);
        g.Tick(0.05);
        CheckTrue("the merge point blows the tick power reaches it", a1.IsPopped);
        CheckTrue("and only the merge point", popped.Count == 1 && popped[0] == "A1");

        for (int t = 0; t < 20; t++) g.Tick(0.05);
        CheckTrue("the rest of the ring is left standing", !a2.IsPopped && !b1.IsPopped && !b2.IsPopped);
        Check("and power no longer circulates", a2.Inflow + b1.Inflow + b2.Inflow, 0);
    }

    static void UnfedRingStaysQuiet()
    {
        Console.WriteLine("a ring nothing feeds stays quiet");
        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var a = g.AddNode("a"); var b = g.AddNode("b"); var c = g.AddNode("c");
        g.Connect(a, b); g.Connect(b, c); g.Connect(c, a);
        for (int t = 0; t < 20; t++) g.Tick(0.05);
        CheckTrue("nothing blows", !a.IsPopped && !b.IsPopped && !c.IsPopped);
    }

    // source -> c1 -> cell -> c2 -> c1: a loop on paper, but the cell does not forward.
    static void ComponentsBreakRings()
    {
        Console.WriteLine("a component in a loop means it is not a ring");
        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var src = g.AddSource("source", 100);
        var c1 = g.AddNode("c1");
        var store = new BatteryBehaviour(100, 50, 1e6, 0);
        var cell = g.AddSource("cell", store, store);
        var c2 = g.AddNode("c2");
        g.Connect(src, c1); g.Connect(c1, cell); g.Connect(cell, c2); g.Connect(c2, c1);
        for (int t = 0; t < 40; t++) g.Tick(0.05);
        CheckTrue("nothing blows", !c1.IsPopped && !c2.IsPopped);
        CheckTrue("Validate reports no ring", g.Validate().Count == 0);
    }

    static void OpenSwitchBreaksARing()
    {
        Console.WriteLine("an open switch breaks a ring");
        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var src = g.AddSource("source", 100);
        var a = g.AddNode("a"); var b = g.AddNode("b"); var sw = g.AddNode("switch");
        g.Connect(src, a); g.Connect(a, b); g.Connect(b, sw); g.Connect(sw, a);
        sw.On = false;
        for (int t = 0; t < 20; t++) g.Tick(0.05);
        CheckTrue("open, nothing blows", !a.IsPopped);
        sw.On = true;
        for (int t = 0; t < 4; t++) g.Tick(0.05);
        CheckTrue("closed, the merge point blows", a.IsPopped);
    }

    static void RingTakesAllOfAMergePointsIntegrity()
    {
        Console.WriteLine("a merge point with integrity is wrecked, not just dented");
        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var src = g.AddSource("source", 100);
        var a = g.AddNode("a"); var b = g.AddNode("b");
        a.Integrity = new Integrity(100);
        g.Connect(src, a); g.Connect(a, b); g.Connect(b, a);
        for (int t = 0; t < 4; t++) g.Tick(0.05);
        CheckTrue("its integrity is gone", a.Integrity.IsDestroyed);
        CheckTrue("and it is severed", a.IsPopped);
    }

    static void CycleDetected()
    {
        Console.WriteLine("rings are reported");
        var g = new PowerGraph();
        var r = g.AddSource("source", 1000);
        var a = g.AddNode("a");
        var b = g.AddNode("b");
        g.Connect(r, a); g.Connect(a, b); g.Connect(b, a);
        var problems = g.Validate();
        bool named = false;
        foreach (var p in problems) if (p.StartsWith("ring")) named = true;
        CheckTrue("Validate() names the ring", named);

        var clean = new PowerGraph();
        var r2 = clean.AddSource("source", 1000);
        var x = clean.AddNode("x", new LoadBehaviour(100, 1e9));
        clean.Connect(r2, x);
        CheckTrue("a sound grid reports no problems", clean.Validate().Count == 0);
    }

    // A source whose output changes tick to tick, which is the whole reason it is an interface.
    static void VaryingSource()
    {
        Console.WriteLine("a source can change its own output");
        var ramp = new RampProducer(0, 100);         // climbs 100 W per second
        var g = new PowerGraph();
        var src = g.AddSource("source", new ProducerSource(ramp));
        var load = g.AddNode("load");
        g.Connect(src, load);

        for (int i = 0; i < 100; i++) g.Tick(0.01);  // one second
        Check("output after 1 s of ramp", ramp.WattsProduced, 100, 1e-6);
        // Offered records what the source put out DURING the last tick, before that tick advanced
        // production, so a ramping producer reads exactly one tick behind its live output.
        Check("grid recorded the previous tick's output", g.TotalInjected, 99, 1e-6);

        for (int i = 0; i < 100; i++) g.Tick(0.01);
        Check("output after 2 s of ramp", ramp.WattsProduced, 200, 1e-6);
        CheckTrue("the source tracked its own output", ramp.TotalJoules > 0);
    }

    // One node can produce and consume, which is what a battery is.
    static void BatteryIsBoth()
    {
        Console.WriteLine("a node can be source and sink at once");
        var cell = new ConstantSourceBehaviour(200);
        var charge = new LoadBehaviour(50, 1e9);
        var g = new PowerGraph();
        var battery = g.AddSource("battery", cell, charge);
        var load = g.AddNode("load", new LoadBehaviour(100, 1e9));
        g.Connect(battery, load);
        g.Settle();

        Check("battery offers", battery.Offered, 200);
        Check("it cannot charge from its own output", battery.Drawn, 0);
        Check("so the whole offer goes downstream", battery.Passed, 200);
        Check("load takes what it is rated for", load.Drawn, 100);
        Check("surplus dumps at the load", load.Dumped, 100);
        CheckTrue("no validation complaints", g.Validate().Count == 0);
    }

    // A reaction cannot be switched off by a switch, so isolating it turns its output into heat.
    static void ProducerCannotDecline()
    {
        Console.WriteLine("an isolated producer cooks itself");
        var core = new RampProducer(500, 0);
        var g = new PowerGraph();
        var src = g.AddSource("core", new ProducerSource(core));
        var load = g.AddNode("load", new LoadBehaviour(200, 1e9));
        var cable = g.Connect(src, load);
        g.Settle();

        Check("feeding the grid", load.Drawn, 200);
        Check("no heat at the core", src.Dumped, 0);

        cable.Enabled = false;                       // isolate it
        g.Settle();

        Check("still producing", src.Offered, 500);
        Check("and all of it is now heat at the core", src.Dumped, 500);
        CheckTrue("produced equals delivered plus heat", Math.Abs(500 - (0 + src.Dumped)) < 1e-9);
    }

    // A store is allowed to decline, so isolating one costs it nothing.
    static void StoreHoldsWhenIsolated()
    {
        Console.WriteLine("an isolated store keeps its charge");
        var store = new BatteryBehaviour(0, 100, 1000);    // no intake, 100 W out, 1000 J, starts full
        var g = new PowerGraph();
        var cell = g.AddSource("cell", store);
        var load = g.AddNode("load", new LoadBehaviour(100, 1e9));
        var cable = g.Connect(cell, load);

        for (int i = 0; i < 100; i++) g.Tick(0.05);  // 5 s of supplying
        Check("it drained by what it supplied", store.Charge, 500, 1.0);
        Check("no heat at the cell", cell.Dumped, 0);

        cable.Enabled = false;                       // isolate it
        double held = store.Charge;
        for (int i = 0; i < 200; i++) g.Tick(0.05);  // 10 s isolated

        Check("charge untouched", store.Charge, held);
        Check("offering nothing", cell.Offered, 0);
        Check("and wasting nothing", cell.Dumped, 0);
    }

    // A store takes in and puts out in the same tick, which is the exception to endpoints-only.
    // A cell nearly empty and trickle-fed must not offer its full rating: that makes power from nothing.
    static void EmptyStoreCannotSupplyMoreThanItGets()
    {
        Console.WriteLine("a nearly empty cell cannot supply more than it is given");
        var store = new BatteryBehaviour(100, 100, 1000, 0.1);
        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var src = g.AddSource("trickle", 2);
        var cell = g.AddSource("cell", store, store);
        var load = g.AddNode("load", new LoadBehaviour(100, 1e9));
        g.Connect(src, cell);
        g.Connect(cell, load);

        double delivered = 0.0, fed = 0.0;
        for (int t = 0; t < 400; t++)
        {
            g.Tick(0.05);
            delivered += load.Drawn * 0.05;
            fed += cell.Drawn * 0.05;
        }
        Console.WriteLine($"         delivered {delivered:0.##} J, held 0.1 J + fed {fed:0.##} J");
        CheckTrue("over 20 s it delivers no more than it held plus what it was fed", delivered <= fed + 0.1 + 1e-9);
        Check("so it settles at the trickle", cell.Offered, 2, 0.01);
    }

    static void StoreChargesAndSuppliesAtOnce()
    {
        Console.WriteLine("a store charges from its input while supplying its output");
        var store = new BatteryBehaviour(100, 100, 1000, 500);   // 100 W in, 100 W out, half full
        var g = new PowerGraph();
        var src = g.AddSource("source", 200);
        var cell = g.AddSource("cell", store, store);       // both faces on one node
        var load = g.AddNode("load", new LoadBehaviour(100, 1e9));
        g.Connect(src, cell);
        g.Connect(cell, load);
        g.Settle();

        Check("arriving from upstream", cell.Arriving, 200);
        Check("charging from the input", cell.Drawn, 100);
        Check("offering to the output", cell.Offered, 100);
        Check("passing on only its own output", cell.Passed, 100);
        Check("what it cannot bank is heat at the cell", cell.Dumped, 100);
        Check("load takes its rating", load.Drawn, 100);
        Check("nothing is lost", cell.Arriving + cell.Offered, cell.Drawn + cell.Passed + cell.Dumped);
    }

    // Turning a component off takes it out of the graph, so the cable before it becomes the dead end.
    static void SwitchedOffNodeRejectsPower()
    {
        Console.WriteLine("a node that is off rejects power");
        var lamp = new LoadBehaviour(50, 1e9);
        var g = new PowerGraph();
        var src = g.AddSource("source", 100);
        var cable = g.AddNode("cable");
        var bulb = g.AddNode("bulb", lamp);
        g.Connect(src, cable);
        g.Connect(cable, bulb);
        g.Settle();

        Check("on: bulb draws its rating", bulb.Drawn, 50);
        Check("on: bulb wastes the rest", bulb.Dumped, 50);
        Check("on: cable passes it all through", cable.Passed, 100);
        Check("on: cable wastes nothing", cable.Dumped, 0);

        bulb.On = false;
        g.Settle();

        Check("off: nothing reaches the bulb", bulb.Inflow, 0);
        Check("off: bulb draws nothing", bulb.Drawn, 0);
        Check("off: bulb wastes nothing", bulb.Dumped, 0);
        Check("off: the cable is now the dead end", cable.Dumped, 100);
        Check("off: source still pushing", src.Offered, 100);

        // and what it was holding fades rather than freezing
        var fading = new LoadBehaviour(50, 50);
        var g2 = new PowerGraph();
        var s2 = g2.AddSource("source", 50);
        var lamp2 = g2.AddNode("lamp", fading);
        g2.Connect(s2, lamp2);
        for (int i = 0; i < 100; i++) g2.Tick(0.05);
        CheckTrue("lit while fed", fading.Working);

        lamp2.On = false;
        double t2 = 0.0;
        while (fading.Working && t2 < 5.0) { g2.Tick(0.05); t2 += 0.05; }
        Check("switched off, it coasts down over capacity/drain", t2, 1.0, 0.1);
        CheckTrue("then dark", !fading.Working);
    }

    // Waste heats the dead end, then travels back along the cables toward the source.
    static void HeatTravelsBackUpTheRun()
    {
        Console.WriteLine("heat conducts back up the run");
        var g = new PowerGraph();
        var src = g.AddSource("source", 100);
        var run = new PowerNode[5];
        var prev = src;
        for (int i = 0; i < 5; i++)
        {
            run[i] = g.AddNode("cable" + i);
            g.Connect(prev, run[i]);
            prev = run[i];
        }
        // nothing on the end, so cable4 is the dead end and takes the whole 100 W
        for (int t = 0; t < 400; t++) g.Tick(0.05);   // 20 s

        CheckTrue("the dead end is the hottest tile on the run", run[4].Celsius > run[0].Celsius);
        CheckTrue($"dead end above ambient ({run[4].Celsius:0.#} C)", run[4].Celsius > PowerGraph.AmbientCelsius + 1);
        CheckTrue("heat reached the cable before it", run[3].Celsius > PowerGraph.AmbientCelsius + 1);
        CheckTrue("and travelled the whole run", run[0].Celsius > PowerGraph.AmbientCelsius + 1);
        CheckTrue("and the one before that", run[2].Celsius > PowerGraph.AmbientCelsius + 1);
        CheckTrue("gradient falls away from the dead end",
            run[4].Celsius > run[3].Celsius && run[3].Celsius > run[2].Celsius
            && run[2].Celsius > run[1].Celsius && run[1].Celsius > run[0].Celsius);
        Console.WriteLine($"         profile: {run[0].Celsius:0.#} {run[1].Celsius:0.#} {run[2].Celsius:0.#} {run[3].Celsius:0.#} {run[4].Celsius:0.#} C  (dead end on the right)");
    }

    // A sound grid drifts back to ambient on its own.
    static void HeatShedsWhenNothingIsWasted()
    {
        Console.WriteLine("a grid that wastes nothing cools off");
        var lamp = new LoadBehaviour(100, 1e9);
        var g = new PowerGraph();
        var src = g.AddSource("source", 100);
        var cable = g.AddNode("cable");
        var load = g.AddNode("lamp", lamp);
        g.Connect(src, cable);
        g.Connect(cable, load);

        for (int t = 0; t < 200; t++) g.Tick(0.05);
        Check("consumption matches supply, so nothing is wasted", g.TotalDumped, 0);
        Check("cable sits at ambient", cable.Celsius, PowerGraph.AmbientCelsius);

        cable.Celsius = 80.0;                         // pretend it was hot
        for (int t = 0; t < 2000; t++) g.Tick(0.05);
        Check("and it sheds back to ambient", cable.Celsius, PowerGraph.AmbientCelsius);
    }

    // The dial says 30 s for a 100 W tile at 120 C, so that is what it should average.
    static void PopTimingMatchesTheDial()
    {
        Console.WriteLine("pop timing matches the seconds-to-fail dial");

        double MeanSecondsToPop(double watts, double celsius, double dialSeconds, int trials)
        {
            double total = 0.0;
            for (int t = 0; t < trials; t++)
            {
                var g = new PowerGraph
                {
                    PopSeed = 1000 + t,
                    PopSecondsAt100WAnd120C = dialSeconds,
                    DegreesPerWastedWattSecond = 0.0,   // hold the temperature still for the measurement
                    CoolingDegreesPerSecond = 0.0,
                    ConductionPerSecond = 0.0,
                };
                var src = g.AddSource("source", watts);
                var cable = g.AddNode("cable");
                g.Connect(src, cable);
                for (int w = 0; w < 4; w++) g.Tick(0.05);   // let the flow reach it
                cable.Celsius = celsius;

                double elapsed = 0.0;
                while (!cable.IsPopped && elapsed < dialSeconds * 40.0)
                {
                    g.Tick(0.05);
                    elapsed += 0.05;
                }
                total += elapsed;
            }
            return total / trials;
        }

        double mean30 = MeanSecondsToPop(100, 120, 30.0, 400);
        Console.WriteLine($"         100 W at 120 C, dial 30 s  ->  mean {mean30:0.#} s over 400 runs");
        Check("averages the dial", mean30, 30.0, 4.0);

        double mean5 = MeanSecondsToPop(100, 120, 5.0, 400);
        Console.WriteLine($"         same, dial 5 s             ->  mean {mean5:0.#} s");
        Check("a smaller dial is proportionally faster", mean5, 5.0, 1.0);

        double half = MeanSecondsToPop(50, 120, 30.0, 400);
        Console.WriteLine($"         50 W at 120 C, dial 30 s   ->  mean {half:0.#} s");
        CheckTrue("half the power lasts roughly twice as long", half > mean30 * 1.6 && half < mean30 * 2.5);
    }

    // Below the floor nothing blows, whatever it is carrying.
    static void CoolTilesNeverPop()
    {
        Console.WriteLine("a cool tile never pops");
        var g = new PowerGraph
        {
            DegreesPerWastedWattSecond = 0.0,
            CoolingDegreesPerSecond = 0.0,
            ConductionPerSecond = 0.0,
        };
        var src = g.AddSource("source", 5000);
        var cable = g.AddNode("cable");
        g.Connect(src, cable);
        for (int w = 0; w < 4; w++) g.Tick(0.05);
        cable.Celsius = g.PopFloorCelsius - 1.0;

        for (int t = 0; t < 20000; t++) g.Tick(0.05);        // 1000 s carrying 5 kW
        CheckTrue($"5 kW at {cable.Celsius:0} C survived 1000 s", !cable.IsPopped);
    }

    // A blown tile carries neither power nor heat, and cannot be switched back on.
    static void PoppedTileStopsEverything()
    {
        Console.WriteLine("a popped tile stops carrying power and heat");
        // conduction off, so only the tile under test gets hot and only it can blow
        var g = new PowerGraph { ConductionPerSecond = 0.0 };
        var src = g.AddSource("source", 100);
        var a = g.AddNode("cable a");
        var b = g.AddNode("cable b");
        g.Connect(src, a);
        g.Connect(a, b);
        g.Settle();
        Check("before: power reaches the end", b.Inflow, 100);

        PowerNode reported = null;
        g.Popped += n => reported = n;

        b.Celsius = 400.0;                                   // force it to blow shortly
        int guard = 0;
        while (!b.IsPopped && guard++ < 20000) g.Tick(0.05);
        CheckTrue("it blew", b.IsPopped);
        CheckTrue("and the event named it", reported == b);

        g.Settle();
        Check("nothing reaches it any more", b.Inflow, 0);
        Check("and the cable before it is the new dead end", a.Dumped, 100);

        b.On = true;
        g.Settle();
        Check("flipping a switch cannot bring it back", b.Inflow, 0);

        // a severed tile also stops carrying heat, unlike a merely switched-off one
        var h = new PowerGraph { DegreesPerWastedWattSecond = 0.0, CoolingDegreesPerSecond = 0.0 };
        var hs = h.AddSource("source", 10);
        var warm = h.AddNode("warm");
        var cold = h.AddNode("cold");
        h.Connect(hs, warm);
        h.Connect(warm, cold);
        h.Settle();
        warm.Celsius = 300.0;
        cold.IsPopped = true;
        double before = warm.Celsius;
        for (int t = 0; t < 40; t++) h.Tick(0.05);
        Check("no heat crosses into a severed tile", cold.Celsius, PowerGraph.AmbientCelsius);
        CheckTrue("so it can only travel upstream", hs.Celsius > PowerGraph.AmbientCelsius + 1);
        CheckTrue("and the warm tile sheds only that way", warm.Celsius < before);
    }

    // Three branches sized exactly to their share run cool. Lose one and the survivors are
    // over-supplied, so they heat - and that heat conducts back toward the source, which carries
    // every watt on the grid. A cascade in a tree decapitates rather than spreading outward.
    static void LosingASiblingKillsTheSource()
    {
        Console.WriteLine("losing one branch kills the source, not the survivors");

        var g = new PowerGraph { PopSeed = 7 };
        var src = g.AddSource("source", 999);
        var trunk = g.AddNode("trunk");
        g.Connect(src, trunk);

        var loads = new PowerNode[3];
        for (int i = 0; i < 3; i++)
        {
            loads[i] = g.AddNode("branch" + i, new LoadBehaviour(333, 1e9));
            g.Connect(trunk, loads[i]);
        }
        g.Settle();

        Check("each branch gets its third", loads[0].Inflow, 333);
        Check("and uses all of it", loads[0].Drawn, 333);
        Check("so nothing is wasted anywhere", g.TotalDumped, 0);

        // knock one out from outside, as a hull breach or a cut cable would
        loads[2].IsPopped = true;

        double elapsed = 0.0;
        var order = new List<string>();
        g.Popped += n => order.Add($"{n.Name} at {elapsed:0.#} s ({n.Celsius:0} C, carrying {n.Inflow:0} W)");

        while (order.Count < 3 && elapsed < 600.0)
        {
            g.Tick(0.05);
            elapsed += 0.05;
        }

        foreach (var line in order) Console.WriteLine("         blew: " + line);
        Console.WriteLine($"         after: trunk popped={trunk.IsPopped}, " +
                          $"branches {loads[0].IsPopped}/{loads[1].IsPopped}, " +
                          $"grid carrying {g.TotalDrawn + g.TotalDumped:0} W");

        CheckTrue("something blew", order.Count > 0);
        // Pop chance is power x heat, and a source carries everything it produces, so the source is
        // the most stressed tile on any grid. Whether that should be poppable at all is undecided.
        CheckTrue("the highest-current tile blew, which is the source", src.IsPopped);
        CheckTrue("the branches that caused it survived", !loads[0].IsPopped && !loads[1].IsPopped);
        Check("and the grid is dead, all at once", g.TotalDrawn + g.TotalDumped, 0);
    }

    // A thermal failure severs a bare conduit but only damages a component.
    static void ComponentsTakeDamageNotPops()
    {
        Console.WriteLine("components take damage where conduits sever");

        var g = new PowerGraph { PopSeed = 3, ConductionPerSecond = 0.0 };
        var src = g.AddSource("reactor", 1000);
        src.Integrity = new Integrity(100);                 // the reactor is a component
        var cable = g.AddNode("cable");                       // bare conduit, no durability
        g.Connect(src, cable);
        g.Settle();

        var damaged = new List<string>();
        var lost = new List<string>();
        var popped = new List<string>();
        g.Damaged += n => damaged.Add($"{n.Name} -> {n.Integrity}");
        g.Lost += n => lost.Add(n.Name);
        g.Popped += n => popped.Add(n.Name);

        src.Celsius = 300.0;
        for (int t = 0; t < 400 && !src.Integrity.IsDestroyed; t++)
        {
            g.Tick(0.05);
            if (src.Celsius < 300.0) src.Celsius = 300.0;     // hold it in the fire
        }

        foreach (var d in damaged) Console.WriteLine("         " + d);
        Check("four hits ended it", damaged.Count, 4);
        Check("and it reported the loss once", lost.Count, 1);
        CheckTrue("the reactor never 'popped' - only the bare cable did", !popped.Contains("reactor"));
        CheckTrue("it is not severed, it is spent", !src.IsPopped && src.Integrity.IsDestroyed);
        g.Settle();
        Check("a spent component produces nothing", src.Offered, 0);

        // it is wrecked, not removed: still wired in, still fed, still wasting
        var w = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var feed = w.AddSource("feed", 200);
        var pump = w.AddNode("pump", new LoadBehaviour(80, 1e9));
        pump.Integrity = new Integrity(100);
        w.Connect(feed, pump);
        w.Settle();
        Check("sound: it draws its rating", pump.Drawn, 80);
        Check("sound: and wastes the rest", pump.Dumped, 120);

        pump.Integrity.TakeDamage(100.0);
        w.Settle();
        CheckTrue("wrecked", pump.IsWrecked);
        CheckTrue("still in the grid", pump.CanReceivePower());
        Check("still being fed", pump.Inflow, 200);
        Check("but draws nothing", pump.Drawn, 0);
        Check("so it wastes the lot and cooks its compartment", pump.Dumped, 200);
    }

    // Below the worn line a component sometimes spends its charge for nothing.
    static void WornComponentsMisfire()
    {
        Console.WriteLine("a worn component misfires");

        int MisfiresAt(double conditionFraction, int cycles)
        {
            var wear = new Integrity(100, seed: 11);
            wear.TakeDamage(100.0 * (1.0 - conditionFraction));

            var gun = new LoadBehaviour(500, 500, 0.0) { Integrity = wear };
            var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
            var src = g.AddSource("source", 500);
            var node = g.AddNode("gun", gun);
            g.Connect(src, node);

            int misfires = 0, fires = 0;
            while (fires + misfires < cycles)
            {
                g.Tick(0.05);
                if (gun.Misfired) misfires++;
                if (gun.Started) { fires++; gun.Discharge(); }
            }
            return misfires;
        }

        Check("a sound component never misfires", MisfiresAt(1.00, 200), 0);
        Check("still nothing just above the worn line", MisfiresAt(0.25, 200), 0);
        int nearlyGone = MisfiresAt(0.02, 200);
        Console.WriteLine($"         at 2% condition: {nearlyGone} misfires in 200 cycles");
        CheckTrue("nearly spent, it fails often", nearlyGone > 40);
    }

    // Each cell asks a harder question than the last, and each answer prevents a failure.
    static void ThreeTiersOfCell()
    {
        Console.WriteLine("three tiers of cell");

        // cell -> cable -> cable -> lamp, with a switch we can open partway along
        PowerGraph Rig(BatteryBehaviour cell, out PowerNode cellNode, out PowerEdge cut, out LoadBehaviour lamp)
        {
            var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
            cellNode = g.AddSource("cell", cell);
            var a = g.AddNode("cable a");
            var b = g.AddNode("cable b");
            lamp = new LoadBehaviour(100, 1e9);
            var load = g.AddNode("lamp", lamp);
            g.Connect(cellNode, a);
            cut = g.Connect(a, b);
            g.Connect(b, load);
            g.Settle();
            return g;
        }

        // --- plain cell: cannot see past its own conduit ---
        var plain = new BatteryBehaviour(0, 100, 100000);
        var g1 = Rig(plain, out var n1, out var cut1, out _);
        Check("plain: supplying", n1.Offered, 100);
        cut1.Enabled = false;                       // switch opened two tiles away
        g1.Settle();
        Check("plain: still pushing into a dead run", n1.Offered, 100);
        CheckTrue("plain: so the cable before the break cooks", g1.TotalDumped > 0);

        // --- safety cell: looks for a component, not just a cable ---
        var safety = new SafetyBatteryBehaviour(0, 100, 100000);
        var g2 = Rig(safety, out var n2, out var cut2, out _);
        Check("safety: supplying", n2.Offered, 100);
        cut2.Enabled = false;
        g2.Settle();
        Check("safety: stops, the run reaches nothing", n2.Offered, 0);
        Check("safety: and nothing is wasted", g2.TotalDumped, 0);

        // --- safety cell still feeds a component that is merely full ---
        var safety2 = new SafetyBatteryBehaviour(0, 100, 100000);
        var g3 = Rig(safety2, out var n3, out _, out var lamp3);
        for (int t = 0; t < 40; t++) g3.Tick(0.05);
        lamp3.Enabled = false;                      // module off: wants nothing, but is still there
        g3.Settle();
        CheckTrue("safety: keeps pushing at a sated grid", n3.Offered > 0);

        // --- smart cell: reads demand, and has a dial ---
        var smart = new SmartBatteryBehaviour(0, 100, 100000);
        var g4 = Rig(smart, out var n4, out _, out var lamp4);
        Check("smart: supplying while something wants it", n4.Offered, 100);

        smart.Throttle = 0.25;
        g4.Settle();
        Check("smart: the dial caps the output", n4.Offered, 25);

        smart.Throttle = 1.0;
        lamp4.Enabled = false;                      // nothing wants anything now
        g4.Settle();
        Check("smart: stops when nothing is asking", n4.Offered, 0);
        Check("smart: so an idle grid wastes nothing", g4.TotalDumped, 0);
    }

    // A reactor is meant to be hot. Damage measures how far over that it has gone, not how hot it is.
    static void ReactorRunsHotWithoutBeingBroken()
    {
        Console.WriteLine("a reactor runs hot without being damaged for it");

        var core = new FissionCore { OutputPerCore = 100, Cores = 1, HeatRatio = 2.0, WorkingCelsius = 400 };
        core.TargetWithdrawal = 1.0;

        var g = new PowerGraph { PopSeed = 2 };
        var reactor = g.AddSource("reactor", new ReactorBehaviour(core, null));
        reactor.Integrity = new Integrity(100);
        var load = g.AddNode("load", new LoadBehaviour(100, 1e9));
        g.Connect(reactor, load);

        for (int t = 0; t < 1200; t++) g.Tick(0.05);       // 60 s: rods take 20 s to come up

        Check("rods fully withdrawn", core.Withdrawal, 1.0);
        Check("producing its rating", reactor.Offered, 100);
        Check("and it is sitting at its working temperature", reactor.Celsius, 400, 0.5);
        Check("which reads as zero excess", reactor.HeatAboveBaseline, 0, 0.5);
        Check("so it takes no damage for running", reactor.Integrity.Fraction, 1.0);

        // now shove it above where it should be
        reactor.Celsius = 500.0;
        int hits = 0;
        g.Damaged += n => hits++;
        for (int t = 0; t < 400 && hits == 0; t++) { g.Tick(0.05); if (reactor.Celsius < 500.0) reactor.Celsius = 500.0; }
        CheckTrue($"but 100 C over its baseline does damage it ({hits} hit)", hits > 0);
    }

    // Down is gravity, up is motors.
    static void RodsDropFastAndRiseSlow()
    {
        Console.WriteLine("rods drop fast and rise slow");

        var core = new FissionCore { RaisePerSecond = 0.05, DropPerSecond = 0.5 };
        var g = new PowerGraph();
        var reactor = g.AddSource("reactor", new ReactorBehaviour(core, null));
        g.Connect(reactor, g.AddNode("load", new LoadBehaviour(100, 1e9)));

        core.TargetWithdrawal = 1.0;
        double t1 = 0.0;
        while (core.Withdrawal < 1.0 && t1 < 120.0) { g.Tick(0.05); t1 += 0.05; }
        Check("full withdrawal takes 1/RaisePerSecond seconds", t1, 20.0, 0.2);

        core.Scram();
        double t2 = 0.0;
        while (core.Withdrawal > 0.0 && t2 < 120.0) { g.Tick(0.05); t2 += 0.05; }
        Check("a scram drops them in 1/DropPerSecond", t2, 2.0, 0.2);
        Check("and it stops producing", core.WattsProduced, 0);
        CheckTrue("shutdown is ten times faster than startup", t1 > t2 * 5);
    }

    // Coolant is a consumable, the pump is a ceiling, and nobody turns the dial down for you.
    static void CoolantCostsSupplies()
    {
        Console.WriteLine("coolant costs supplies and the pump is a ceiling");

        var coolant = new CoolantLoop { Litres = 100, MaxFlowLitresPerSecond = 2.0, DegreesPerLitre = 20.0 };
        var core = new FissionCore { WorkingCelsius = 400 };
        core.TargetWithdrawal = 1.0;

        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var reactor = g.AddSource("reactor", new ReactorBehaviour(core, null, coolant));
        g.Connect(reactor, g.AddNode("load", new LoadBehaviour(100, 1e9)));
        for (int t = 0; t < 600; t++) g.Tick(0.05);        // spin up

        Check("drum untouched while the dial is at zero", coolant.Litres, 100);

        coolant.Flow = 1.0;
        for (int t = 0; t < 200; t++) g.Tick(0.05);        // 10 s at 1 L/s
        Check("ten seconds at 1 L/s costs ten litres", coolant.Litres, 90, 0.2);

        coolant.Flow = 10.0;                               // ask for far more than the pump can give
        CheckTrue("the pump reports being maxed", coolant.AtCeiling);
        Check("and only moves what it can", coolant.ActualFlow, 2.0);

        coolant.Flow = 0.0;
        double left = coolant.Litres;
        for (int t = 0; t < 200; t++) g.Tick(0.05);
        Check("dial back to zero, drum stops draining", coolant.Litres, left);

        coolant.Flow = 2.0;
        int guard = 0;
        while (coolant.Litres > 0.0 && guard++ < 20000) g.Tick(0.05);
        Check("run it flat out and it runs dry", coolant.Litres, 0);
        Check("a dry drum moves nothing", coolant.ActualFlow, 0);
    }

    // The magnets are an ordinary load, so the fail-safe is just the grid working normally.
    static void LosingControlPowerDropsTheRods()
    {
        Console.WriteLine("the starter is for starting, not for running");

        var core = new FissionCore { OutputPerCore = 200, Cores = 1, RaisePerSecond = 0.5, DropPerSecond = 0.5 };
        var control = new LoadBehaviour(20, 40);          // 20 W to hold, 40 J of grip once cut
        var reactor = new ReactorBehaviour(core, control);

        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var cell = new BatteryBehaviour(0, 20, 100000);
        var battery = g.AddSource("battery", cell);
        var feed = g.AddNode("control feed");
        var node = g.AddSource("reactor", reactor, reactor);   // both faces on one tile
        var bus = g.AddNode("bus");
        var load = g.AddNode("load", new LoadBehaviour(200, 1e9));
        g.Connect(battery, feed);
        var supply = g.Connect(feed, node);
        g.Connect(node, bus);
        g.Connect(bus, load);

        // cold start: the crew can ask for rods before the magnets grip; they rise once they do
        core.TargetWithdrawal = 1.0;
        g.Tick(0.05);
        CheckTrue("magnets not yet charged", !reactor.RodsHeld);
        Check("the dial stays where the crew put it", core.TargetWithdrawal, 1.0);
        Check("but the rods cannot rise without grip", core.Withdrawal, 0);

        for (int t = 0; t < 400; t++) g.Tick(0.05);      // control charges, then the rods climb
        CheckTrue("magnets holding", reactor.RodsHeld);
        for (int t = 0; t < 400; t++) g.Tick(0.05);
        Check("rods up", core.Withdrawal, 1.0);
        Check("and it is exporting, less its own house load", node.Offered, 180, 0.5);

        // Cutting the starter no longer touches a running core: it holds its own magnets.
        supply.Enabled = false;
        for (int t = 0; t < 600; t++) g.Tick(0.05);       // 30 s with no external supply
        CheckTrue("a running core ignores losing its starter", reactor.RodsHeld);
        Check("rods still up", core.Withdrawal, 1.0);

        // Only a scram puts them down, and then it cannot restart without the starter back.
        reactor.Core.Scram();
        while (core.Withdrawal > 0.0) g.Tick(0.05);
        Check("scrammed", core.Withdrawal, 0);
        for (int t = 0; t < 600; t++) g.Tick(0.05);
        CheckTrue("with no production and no starter, the magnets die", !reactor.RodsHeld);

        core.TargetWithdrawal = 1.0;                      // the crew asks for power back
        for (int t = 0; t < 40; t++) g.Tick(0.05);
        Check("the dial holds what the crew asked for", core.TargetWithdrawal, 1.0);
        Check("but with no grip the rods cannot rise", core.Withdrawal, 0);

        supply.Enabled = true;                            // starter back on
        for (int t = 0; t < 400; t++) g.Tick(0.05);
        CheckTrue("magnets live again", reactor.RodsHeld);
        Check("and the rods climb back to the dial on their own", node.Offered, 180, 0.5);
    }

    // Power fed to the magnets must not leave by the output port.
    static void DiodeStopsControlPowerLeakingOut()
    {
        Console.WriteLine("a component never passes power through");

        // The same tile as a bare conduit and as a 20 W load, each with an output onward.
        double PassedOnBy(bool component)
        {
            var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
            var feed = g.AddSource("feed", 100);
            var box = component ? g.AddNode("box", new LoadBehaviour(20, 1e9)) : g.AddNode("box");
            var downstream = g.AddNode("ship", new LoadBehaviour(500, 1e9));
            g.Connect(feed, box);
            g.Connect(box, downstream);
            g.Settle();
            return downstream.Inflow;
        }

        Check("a bare conduit passes the whole feed on", PassedOnBy(false), 100);
        Check("a component passes none of it on", PassedOnBy(true), 0);

        // and a reactor is a component like any other
        var core2 = new FissionCore { OutputPerCore = 100, Cores = 1 };
        var r2 = new ReactorBehaviour(core2, new LoadBehaviour(20, 40));
        var g2 = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var feed2 = g2.AddSource("feed", 100);
        var rn = g2.AddSource("reactor", r2, r2);
        var ship = g2.AddNode("ship", new LoadBehaviour(500, 1e9));
        g2.Connect(feed2, rn);
        g2.Connect(rn, ship);
        g2.Settle();
        CheckTrue("the reactor does not forward", !rn.ForwardsPower);
        Check("rods down, so nothing reaches the ship", ship.Inflow, 0);
        Check("and the feed it could not use is heat at the reactor", rn.Dumped, 80);
    }

    // The same diode is what stops a cold-start loop compounding on every lap.
    static void DiodeStopsTheColdStartLoopRunningAway()
    {
        Console.WriteLine("a diode stops the cold-start loop running away");

        // reactor -> cable -> cell -> cable -> back to the reactor's control input
        var core = new FissionCore { OutputPerCore = 100, Cores = 1, RaisePerSecond = 1.0 };
        var control = new LoadBehaviour(20, 40);
        var reactor = new ReactorBehaviour(core, control);
        var cell = new BatteryBehaviour(100, 100, 5000, 5000);   // starts full, so it stops absorbing early

        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var rnode = g.AddSource("reactor", reactor, reactor);
        var out1 = g.AddNode("out");
        var cnode = g.AddSource("cell", cell, cell);
        var back = g.AddNode("return");
        g.Connect(rnode, out1);
        g.Connect(out1, cnode);
        g.Connect(cnode, back);
        g.Connect(back, rnode);

        CheckTrue("Validate does not flag it: the reactor and the cell each break the loop", g.Validate().Count == 0);

        for (int t = 0; t < 60; t++) g.Tick(0.05);
        core.TargetWithdrawal = 1.0;

        double peak = 0.0;
        for (int t = 0; t < 4000; t++)     // 200 s
        {
            g.Tick(0.05);
            if (rnode.Inflow > peak) peak = rnode.Inflow;
        }

        Console.WriteLine($"         after 200 s: reactor inflow {rnode.Inflow:0} W, peak {peak:0} W, " +
                          $"produced {core.WattsProduced:0} W, wasted at reactor {rnode.Dumped:0} W");
        CheckTrue("it settles instead of climbing without bound", peak < 1000);
        CheckTrue("the reactor is running", core.WattsProduced > 0);
        Check("and its output carries what the core made less house load",
            rnode.Passed, core.WattsProduced - reactor.HouseLoad, 1.0);
    }

    // Once running, the core holds its own magnets. The starter supply is for starting only.
    static void RunningReactorHoldsItsOwnRods()
    {
        Console.WriteLine("a running reactor holds its own rods");

        var core = new FissionCore { OutputPerCore = 100, Cores = 1, RaisePerSecond = 0.5 };
        var control = new LoadBehaviour(20, 40);
        var reactor = new ReactorBehaviour(core, control);

        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var starter = g.AddSource("starter", 100);
        var feed = g.AddNode("starter feed");
        var node = g.AddSource("reactor", reactor, reactor);
        var ship = g.AddNode("ship", new LoadBehaviour(500, 1e9));
        g.Connect(starter, feed);
        var starterCable = g.Connect(feed, node);
        g.Connect(node, ship);

        // cold: nothing produced, so the magnets must come off the ship
        g.Settle();
        Check("cold: no house load", reactor.HouseLoad, 0);
        CheckTrue("cold: it is asking the grid", reactor.WattsWanted > 0);

        for (int t = 0; t < 200; t++) g.Tick(0.05);
        CheckTrue("magnets holding", reactor.RodsHeld);

        core.TargetWithdrawal = 1.0;
        for (int t = 0; t < 400; t++) g.Tick(0.05);
        Check("rods up", core.Withdrawal, 1.0);

        Check("running: the core covers its own magnets", reactor.HouseLoad, 20, 0.5);
        Check("running: so it asks the grid for nothing", reactor.WattsWanted, 0);
        Check("and exports what is left", node.Offered, 80, 0.5);

        // now pull the starter away entirely
        starterCable.Enabled = false;
        for (int t = 0; t < 600; t++) g.Tick(0.05);      // 30 s with no external supply

        CheckTrue("still holding its rods with no starter", reactor.RodsHeld);
        Check("rods still up", core.Withdrawal, 1.0);
        Check("still exporting", node.Offered, 80, 0.5);
        Check("and nothing arriving is wasted as heat", node.Dumped, 0);
    }

    // Rods travel both ways: a crew throttles a reactor, it is not just on or off.
    static void RodsCanBeLoweredPartWay()
    {
        Console.WriteLine("rods can be lowered as well as raised");

        var core = new FissionCore { OutputPerCore = 100, Cores = 1, RaisePerSecond = 0.5, DropPerSecond = 0.5 };
        var reactor = new ReactorBehaviour(core, new LoadBehaviour(20, 40));

        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var starter = g.AddSource("starter", 100);
        var node = g.AddSource("reactor", reactor, reactor);
        g.Connect(starter, node);
        g.Connect(node, g.AddNode("ship", new LoadBehaviour(500, 1e9)));

        for (int t = 0; t < 200; t++) g.Tick(0.05);
        core.TargetWithdrawal = 1.0;
        for (int t = 0; t < 200; t++) g.Tick(0.05);
        Check("full", core.Withdrawal, 1.0);
        Check("producing", core.WattsProduced, 100);

        core.TargetWithdrawal = 0.5;
        for (int t = 0; t < 200; t++) g.Tick(0.05);
        Check("lowered to half", core.Withdrawal, 0.5, 0.001);
        Check("producing half", core.WattsProduced, 50, 0.5);
        Check("and its working temperature fell with it", node.BaselineCelsius, 210.5, 1.0);

        core.TargetWithdrawal = 0.8;
        for (int t = 0; t < 200; t++) g.Tick(0.05);
        Check("and back up again", core.Withdrawal, 0.8, 0.001);

        core.TargetWithdrawal = 0.0;
        for (int t = 0; t < 200; t++) g.Tick(0.05);
        Check("all the way down", core.Withdrawal, 0);
        Check("making nothing", core.WattsProduced, 0);
    }

    // House load comes off production, so a core throttled below it cannot hold its own rods.
    static void ThrottlingBelowHouseLoadSelfScrams()
    {
        Console.WriteLine("throttling below house load self-scrams");

        var core = new FissionCore { OutputPerCore = 100, Cores = 1, RaisePerSecond = 0.5, DropPerSecond = 0.5 };
        var reactor = new ReactorBehaviour(core, new LoadBehaviour(20, 40));

        var g = new PowerGraph { PopSecondsAt100WAnd120C = 0.0 };
        var starter = g.AddSource("starter", 100);
        var node = g.AddSource("reactor", reactor, reactor);
        var starterCable = g.Connect(starter, node);
        g.Connect(node, g.AddNode("ship", new LoadBehaviour(500, 1e9)));

        for (int t = 0; t < 200; t++) g.Tick(0.05);
        core.TargetWithdrawal = 1.0;
        for (int t = 0; t < 200; t++) g.Tick(0.05);
        starterCable.Enabled = false;                 // running on its own now
        for (int t = 0; t < 200; t++) g.Tick(0.05);
        CheckTrue("self-sustaining at full", reactor.RodsHeld);

        // 30% rods = 30 W produced, more than the 20 W the magnets need
        core.TargetWithdrawal = 0.3;
        for (int t = 0; t < 400; t++) g.Tick(0.05);
        CheckTrue("30% rods still holds (30 W produced vs 20 W needed)", reactor.RodsHeld);
        Check("and exports the difference", node.Offered, 10, 0.5);

        // 10% rods = 10 W produced, less than the magnets need
        core.TargetWithdrawal = 0.1;
        int guard = 0;
        while (reactor.RodsHeld && guard++ < 2000) g.Tick(0.05);
        CheckTrue("10% rods cannot hold its own magnets", !reactor.RodsHeld);

        while (core.Withdrawal > 0.0) g.Tick(0.05);
        Check("so it scrams itself", core.Withdrawal, 0);
        Check("and is making nothing", core.WattsProduced, 0);

        Check("the dial is still where the crew left it", core.TargetWithdrawal, 0.1);
        core.TargetWithdrawal = 1.0;
        for (int t = 0; t < 40; t++) g.Tick(0.05);
        Check("with no grip the rods stay down", core.Withdrawal, 0);
        Check("so it cannot be restarted without the starter", core.WattsProduced, 0);
    }

    // Power moves one hop per tick, which is the surge you see when a conduit is reconnected.
    static void SurgePropagation()
    {
        Console.WriteLine("power travels one hop per tick");
        var g = new PowerGraph();
        var prev = g.AddSource("source", 600);
        var chain = new List<PowerNode>();
        for (int i = 0; i < 5; i++)
        {
            var n = g.AddNode("seg" + i);
            g.Connect(prev, n);
            chain.Add(n);
            prev = n;
        }
        for (int t = 0; t < 6; t++)
        {
            g.Tick(0.02);
            int reached = 0;
            foreach (var n in chain) if (n.Inflow > 0) reached++;
            CheckTrue($"after tick {t + 1}, the wavefront has reached {reached} of 5 segments", reached == t);
        }
        int settleTicks = new PowerGraph() is var _ ? g.Settle() : 0;
        CheckTrue($"settles quickly once filled ({settleTicks} tick(s))", settleTicks <= 2);
    }
}
