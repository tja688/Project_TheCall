using QFramework;
using TheCall;
using System.Text.Json;
// This plans legal choices in an isolated rules host. It is NOT Unity acceptance.
record Input(string Action,string Value="",int Index=0);
sealed class SeedDraw : IDraw {
 readonly Random random; public SeedDraw(int seed){random=new Random(seed);}
 public T Choose<T>(IReadOnlyList<T> values)=>values[random.Next(values.Count)];
 public bool Chance(int percent)=>random.Next(100)<percent;
}
class Program {
 static IArchitecture a;static RunModel r;static LevelModel l;
 static string Book=File.ReadAllText("Assets/StreamingAssets/call-book.json");
 static int Seed;static int LastDay;static int LastEnergy;static List<Input> plan=new();
 static void Replay(){TheCallApp.Reset();ContentGate.Use(ContentBook.Parse(Book));TheCallApp.OnRegisterPatch=x=>x.RegisterUtility<IDraw>(new SeedDraw(Seed));a=TheCallApp.Interface;r=a.GetModel<RunModel>();l=a.GetModel<LevelModel>();foreach(var x in plan)Apply(x);}
 static void Apply(Input x){switch(x.Action){case "opening":a.SendCommand(new KeepOpeningMonsterCommand(x.Value));break;case "begin":a.SendCommand(new BeginLevelCommand());break;case "extraction":a.SendCommand(new PlaceMonsterCommand(x.Value,OperationArea.Extraction,x.Index));break;case "cage":a.SendCommand(new ReturnMonsterCommand(x.Value));break;case "settle":a.SendCommand(new ConfirmSettlementCommand());break;case "leave":a.SendCommand(new LeaveShopCommand());break;case "buy":if(r.ShelfMonsterIds.Contains(x.Value))a.SendCommand(new BuyMonsterCommand(x.Value));else a.SendCommand(new BuyToolCommand(x.Value));break;case "refresh":a.SendCommand(new RefreshShelfCommand());break;}}
 static void Add(Input x){Apply(x);plan.Add(x);}
 static double Value(Monster m){double v=0;foreach(var s in m.Skills)v+=s.Name switch {"镜眼"=>22,"奇异香"=>10,"回响嗓"=>24,"左复制腺体"=>16,"右复制腺体"=>16,"怪异香"=>17,"增生能量体"=>9,"能量体"=>6,"生长能量体"=>s.Quote+8,"优质肉体"=>16,"良好肉体"=>12,"蜜能量体"=>14,"孤岛能量体"=>13,"无穷肌肉"=>20,"电能量体"=>10,"丰产能量体"=>15,"换位手"=>5,"优秀能量体"=>15,"不稳定能量体"=>20,"情绪收集器"=>16,"群能量体"=>15,"自爆体"=>0,_=>Math.Max(0,s.Quote)};return v+(m.Personality=="急躁"?5:0)+(m.Personality=="自律"?6:0);}
 static bool Arrange(Random sampling){Replay();var ids=r.Cage.OrderByDescending(Value).Take(11).Select(m=>m.Id).ToArray();var cap=l.Extraction.Count;string[] best=null;int score=int.MinValue;
 for(int k=0;k<70;k++){Replay();var chosen=k==0?ids.Take(cap).ToArray():ids.OrderBy(_=>sampling.Next()).Take(cap).ToArray();for(int i=0;i<chosen.Length;i++)Apply(new Input("extraction",chosen[i],i));a.SendCommand(new ConfirmSettlementCommand());var produced=r.ProductionLog.LastOrDefault()?.AccumulatedEnergy??0;LastEnergy=produced;if(produced>score){score=produced;best=chosen;}}
 Replay();for(int i=0;i<best.Length;i++)Add(new Input("extraction",best[i],i));Add(new Input("settle"));return r.Phase!=RunPhase.Failed;
 }
 static bool Attempt(){plan.Clear();Replay();var chosen=r.Candidates.OrderByDescending(Value).First();Add(new Input("opening",chosen.Id));var sampling=new Random(Seed+98765);
 for(int day=1;day<=7;day++){LastDay=day;if(day>1)Add(new Input("begin"));if(!Arrange(sampling))return false;
 if(r.Phase==RunPhase.Operation){var ids=l.Extraction.Where(x=>x!=null).ToArray();foreach(var id in ids)Add(new Input("cage",id));if(!Arrange(sampling))return false;}
 if(r.Phase!=RunPhase.Shop)return false;
 // Legal wages fund purchases. Buy strong monsters and passive tools, then breed later in UI fixtures.
 var shelf=a.SendQuery(new ShelfQuery());foreach(var tool in shelf.Tools.OrderByDescending(t=>t.Name switch {"电化能机"=>100,"检测仪"=>95,"新型提取槽"=>90,"复读腺"=>80,"奇异香"=>70,_=>0})){if(tool.Name is "电化能机" or "检测仪" or "新型提取槽" or "复读腺" && tool.Price<=r.Gold)Add(new Input("buy",tool.Name));}
 foreach(var m in shelf.Monsters.OrderByDescending(m=>Value(r.Find(m.Id))).ToArray())if(m.Price<=r.Gold && Value(r.Find(m.Id))>=10)Add(new Input("buy",m.Id));
 Add(new Input("leave"));}
 return r.Phase==RunPhase.Victory;
 }
 static void Main(string[] args){int start=args.Length>0?int.Parse(args[0]):1;int count=args.Length>1?int.Parse(args[1]):100;for(Seed=start;Seed<start+count;Seed++){try{if(Attempt()){Directory.CreateDirectory("docs/audit/2026-10-12/remediation/evidence");File.WriteAllText("docs/audit/2026-10-12/remediation/evidence/natural-plan.json",JsonSerializer.Serialize(new {seed=Seed,actions=plan},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("LEGAL WIN seed="+Seed+" actions="+plan.Count);return;}}catch(Exception ex){Console.WriteLine("seed="+Seed+" error="+ex.ToString());}if(Seed%10==0)Console.WriteLine("searched="+Seed+" lastDay="+LastDay+" energy="+LastEnergy);}Environment.ExitCode=1;}
}
