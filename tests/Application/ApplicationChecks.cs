using System;
using System.Collections.Generic;
using Harvest.Domain;
using Harvest.Application;
var world = new WorldSnapshot(SimulationRules.Default, 7, 1, 1, new[] {new TileState(new GridPosition(0,0))},
    new[] {new TractorState(1,new GridPosition(0,0),Heading.North,ImplementKind.Harvester,10,10,0,10)},Array.Empty<DepotState>(),1,1);
var scripts = new List<ScriptAssignment> {new ScriptAssignment(1,"invalid syntax remains recoverable")};
var save = new StoppedSessionSnapshot(SessionMode.Campaign,"spike-v1","starter-v1",world,0,scripts,1);
scripts.Clear();
if(save.Scripts.Count != 1 || save.World.Tick != 7) throw new Exception("Immutable stopped save failed");
void Reject(Action action) {try {action();} catch(ArgumentException) {return;} throw new Exception("Invalid save accepted");}
Reject(()=>new ScriptAssignment(1,new string('é',40000)));
Reject(()=>new StoppedSessionSnapshot(SessionMode.Sandbox,"spike-v1","starter-v1",world,1,save.Scripts,1));
Reject(()=>new StoppedSessionSnapshot(SessionMode.Campaign,"spike-v1","starter-v1",world,0,save.Scripts,2));
Reject(()=>new StoppedSessionSnapshot(SessionMode.Campaign,"spike-v1","starter-v1",world,0,new[]{save.Scripts[0],save.Scripts[0]},1));
Console.WriteLine("Application stopped DTO checks passed");
