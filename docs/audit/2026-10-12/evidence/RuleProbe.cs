using QFramework;
using TheCall;
using System.Text.Json;
class Draw : IDraw {
 public string Prefer; public bool ChanceResult;
 public T Choose<T>(IReadOnlyList<T> x) { if (Prefer != null) foreach(var v in x) if(v is string s && s==Prefer) return v; return x[0]; }
 public bool Chance(int p)=>ChanceResult;
}
class Program {
 // Run from the repository root. These are isolated rule fixtures, not player-run acceptance.
 static readonly string Root=Path.TrimEndingDirectorySeparator(Environment.GetEnvironmentVariable("THECALL_AUDIT_PROJECT") ?? Directory.GetCurrentDirectory()) + Path.DirectorySeparatorChar;
 static IArchitecture a; static Draw d; static RunModel r; static LevelModel l;
 static void Setup(params string[] tools){TheCallApp.Reset(); ContentGate.Use(ContentBook.Parse(File.ReadAllText(Root+"Assets/StreamingAssets/call-book.json"))); d=new Draw(); TheCallApp.OnRegisterPatch=app=>app.RegisterUtility<IDraw>(d); a=TheCallApp.Interface; r=a.GetModel<RunModel>(); l=a.GetModel<LevelModel>(); r.EnterOperation(1);l.BeginLevel(50,60); foreach(var t in tools)r.AddTool(t); }
 static Monster M(int cell,string[] skills,string p="稳定",string mood="普通",string race="森林",IReadOnlyList<string> parents=null){var m=r.CreateMonster(skills, personality:p,mood:mood,race:race,parentIds:parents); if(cell>=0) l.Put(OperationArea.Extraction,cell,m.Id);else r.AddToCage(m);return m;}
 static SettlementLanding[] Score(){a.SendCommand(new ConfirmSettlementCommand());return a.SendQuery(new SettlementRecordQuery()).OfType<SettlementLanding>().ToArray();}
 static void Out(string name,object data)=>Console.WriteLine(JsonSerializer.Serialize(new{name,data},new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
 static void Main(){
  Setup(); M(0,new[]{"能量体"}); Out("baseline",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup(); M(0,new[]{"回响嗓"});var muscle=M(1,new[]{"无穷肌肉"});Out("new-active-exec",new{landings=Score().Select(x=>new{x.SkillName,x.Energy}),skills=muscle.Skills.Select(x=>new{x.Name,x.IsTemporary})});r.ClearTemporarySkills();Out("muscle-after-clear",muscle.Skills.Select(x=>x.Name));
  Setup(); M(0,new[]{"回响嗓"});M(1,new[]{"收尸腺"});M(2,new[]{"自爆体"});Out("corpse-echo",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup();M(0,new[]{"蜜能量体"});M(2,new[]{"能量体"});Out("honey-gap",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup("精神操控仪");M(0,new[]{"奇异香"},"自私");M(1,new[]{"能量体"});Out("mind-negative",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup();var pa=M(0,new[]{"能量体"},"重视血脉");var ch=M(1,new[]{"能量体"},parents:new[]{pa.Id});Out("kin-positive",Score().Select(x=>new{x.MonsterId,x.SkillName,x.Energy}));
  Setup();pa=M(0,new[]{"能量体"},"仇恨血脉");ch=M(1,new[]{"能量体"},parents:new[]{pa.Id});Out("kin-negative",Score().Select(x=>new{x.MonsterId,x.SkillName,x.Energy}));
  Setup("劣胜装置");M(0,new[]{"能量体"});Out("single-tool",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup("复读腺");pa=M(-1,new[]{"能量体"});ch=M(-1,new[]{"能量体"}); r.TryRemoveFromCage(pa.Id,out _);r.TryRemoveFromCage(ch.Id,out _);l.Put(OperationArea.Breeding,0,pa.Id);l.Put(OperationArea.Breeding,1,ch.Id);l.LockBreeding();a.GetSystem<BreedingSystem>().Spawn();Out("breeding-duplicate",r.Cage.Last().Skills.Select(x=>x.Name));
  Setup();var src=M(-1,new[]{"能量体"},"乐天派");a.SendCommand(new BeginLevelCommand());Out("initial-fixed-mood",new{src.Personality,src.Mood});
  Setup();M(0,new[]{"能量体"},"孤僻");var mm=r.Find(l.Extraction[0]);var detail=a.SendQuery(new MonsterDetailsQuery(mm.Id));Out("preview-isolated",new{preview=detail.Skills[0].Sentence,actual=Score().Select(x=>new{x.SkillName,x.Energy})});
  Setup();M(0,new[]{"能量体","自爆体"},"安于现状");Out("per-skill-lock",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup();r.AddUnlockedTech("基因仓储");r.PutSkillInSlot("能量体");r.PutSkillInSlot("能量体");r.PutSkillInSlot("能量体");var disc=M(-1,new[]{"能量体"});var discardDrop=new OperationDrop(DropPayload.Monster(disc.Id),new DropLanding(LandingPlace.Discard,0,null));Out("expanded-discard-drag",new{capacity=r.GeneCapacity,accepted=a.SendCommand(new CommitOperationDropCommand(discardDrop)),slots=r.GeneSlots.Count,alive=r.Find(disc.Id)!=null});a.SendCommand(new DiscardMonsterCommand(disc.Id));Out("expanded-discard-direct",new{slots=r.GeneSlots.Count,alive=r.Find(disc.Id)!=null});
  Setup();var install=M(0,new[]{"能量体"},"迷恋死亡");r.PutSkillInSlot("左能量体");var drop=new OperationDrop(DropPayload.SkillChip(0),new DropLanding(LandingPlace.Extraction,0,install.Id));Out("install-drag",new{accepted=a.SendCommand(new CommitOperationDropCommand(drop)),skills=install.Skills.Count,genes=r.GeneSlots.Count});Out("install-command",new{accepted=a.SendCommand(new InstallGeneCommand(install.Id,0)),skills=install.Skills.Select(x=>new{x.Name,x.IsTemporary}),genes=r.GeneSlots.Count});
  Setup("复读腺");pa=M(-1,new[]{"能量体"},"安于现状");ch=M(-1,new[]{"左能量体"});r.TryRemoveFromCage(pa.Id,out _);r.TryRemoveFromCage(ch.Id,out _);l.Put(OperationArea.Breeding,0,pa.Id);l.Put(OperationArea.Breeding,1,ch.Id);r.PutSkillInSlot("能量体");r.TryReserveGeneSlot(0,0);l.TryPutSkill(0,"能量体");l.LockBreeding();d.Prefer="能量体";d.ChanceResult=true;a.GetSystem<BreedingSystem>().Spawn();Out("gene-provenance-trim",new{child=r.Cage.Last().Skills.Select(x=>x.Name),geneCount=r.GeneSlots.Count});
  Setup();var candidate=r.Candidates[0];candidate.ChangePersonality("乐天派");a.SendCommand(new KeepOpeningMonsterCommand(candidate.Id));Out("opening-fixed-mood",new{candidate.Personality,candidate.Mood});
  Setup();M(0,new[]{"能量体"},"急躁");mm=r.Find(l.Extraction[0]);detail=a.SendQuery(new MonsterDetailsQuery(mm.Id));Out("preview-impatient",new{preview=detail.Skills[0].Sentence,actual=Score().Select(x=>new{x.SkillName,x.Energy})});
  Setup();M(0,new[]{"能量体"},"热爱工作",mood:"热爱");mm=r.Find(l.Extraction[0]);detail=a.SendQuery(new MonsterDetailsQuery(mm.Id));Out("preview-mood",new{preview=detail.Skills[0].Sentence,actual=Score().Select(x=>new{x.SkillName,x.Energy})});  Setup();M(0,new[]{"祭品腺"});M(1,new[]{"血祭体"});M(2,new[]{"收尸腺"});Out("sacrifice-per-target",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup();M(0,new[]{"啃食体"},"急躁");Out("devour-no-target-buff",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup();var honey=M(0,new[]{"蜜能量体"});M(1,new[]{"能量体"});Out("honey-round1",Score().Select(x=>new{x.SkillName,x.Energy}));a.SendCommand(new ReturnMonsterCommand(honey.Id));Out("honey-round2",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup();M(0,new[]{"回响嗓"});M(1,new[]{"蜜能量体","能量体"});M(2,new[]{"能量体"});Out("echo-active-order",Score().Select(x=>new{x.MonsterId,x.SkillName,x.Energy}));
  Setup("复读腺");M(0,new[]{"镜眼","镜眼"});M(1,new[]{"能量体"});Out("duplicate-mirror",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup("复读腺");M(0,new[]{"增生能量体"});M(1,new[]{"增生能量体","增生能量体"});Out("duplicate-extra",Score().Select(x=>new{x.MonsterId,x.SkillName,x.Energy}));  Setup();var am=M(0,new[]{"能量体"});var bm=M(1,new[]{"换位手","能量体"});M(2,new[]{"镜眼"});Out("opening-aura-target",Score().Select(x=>new{x.MonsterId,x.SkillName,x.Energy}));
  Setup();M(0,new[]{"优质肉体","不稳定肉体","能量体"});var breeder=M(-1,new[]{"左能量体"});r.TryRemoveFromCage(breeder.Id,out _);l.Put(OperationArea.Breeding,0,breeder.Id);Out("opening-skillcount",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup();M(0,new[]{"收尸腺"});M(1,new[]{"抽杀体"});var legacy=M(2,new[]{"祭品腺"});d.Prefer=legacy.Id;Out("death-listener-position",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup();var income=M(0,new[]{"能量体"});income.AddModifier(45);Score();Out("log-no-excess",new{tech=r.TechPoints,text=ProductionLogText.Format(r.ProductionLog)});
  Setup("蓄热熔炉");l.AddEnergy(10);income=M(0,new[]{"能量体"});income.AddModifier(20);Score();Score();Out("log-cumulative",new{text=ProductionLogText.Format(r.ProductionLog)});  Setup("电化能机");M(0,new[]{"奇异香"});Out("monster-scope-global-tool",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup("电化能机");M(0,new[]{"祭品腺"});M(1,new[]{"血祭体"});Out("legacy-first-event-global-tool",Score().Select(x=>new{x.SkillName,x.Energy}));
  Setup("复读腺");var cop=M(0,new[]{"不稳定肉体","能量体"});breeder=M(-1,new[]{"能量体"});r.TryRemoveFromCage(breeder.Id,out _);l.Put(OperationArea.Breeding,0,breeder.Id);Score();Out("copy-with-duplicate-tool",cop.Skills.Select(x=>new{x.Name,x.IsTemporary})); }
}





