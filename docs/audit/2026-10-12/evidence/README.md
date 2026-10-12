# 质检证据与复现

被审查版本：HEAD `a9b964197ae56546376f895d5e8fa11881adfe25`，远端dev `5f177a70dad5940198fe64b6cb38a769c973cfe1`。记录日期2026-10-12。游戏实现未在本次修改。

| 文件 | 用途 |
| --- | --- |
| RuleProbe.cs / RuleProbe.csproj | 当前同源Core+QfSubset的独立规则造景探针，不进入Unity Assets；不作为真实玩家整局验收 |
| rule-probe-baseline.jsonl | 初次36条实际输出，包含正确基线与错误反例 |
| rule-probe-replay.jsonl | 从仓库保存的探针重新运行，36条与baseline完全一致 |
| kernel-tests.txt | 当前245/245已有规则测试通过的实际输出 |
| page-build.txt | 当前Page构建成功的实际输出 |
| unity-editmode-tests.json | Unity CLI相关编辑模式328/328测试结果和逐测试记录 |
| unity-saved-assets.json | 工具18个相同坐标、科技五节点、15个正式性格缺图、18工具图非空、39/6运行目录、保存旧目录不可加载 |
| unity-prefabs.json | UI三份+MonsterSlotCard六份逐份Prefab加载，无缺脚本；Rig39无missing |
| unity-console.json | 本次检查开始cursor之后错误为空，编译失败false；警告2需结项时归因 |

从仓库根目录运行：

```powershell
dotnet run --project docs/audit/2026-10-12/evidence/RuleProbe.csproj
dotnet test kernel/TheCall.Rules.Tests/TheCall.Rules.Tests.csproj --no-restore
dotnet build kernel/TheCall.Page/TheCall.Page.csproj --no-restore
```

探针只打印实际值；**exit 0只表示探针正常运行，不能解释为spec通过**。正确期望与差异在 [Spec轴明细](../spec-axis-findings.md)。整改阶段把反例迁入正式回归断言，禁止把错误baseline作为新期望。

探针初始局面允许设置Model；这是局部规则隔离测试。七天整局验收必须由真实玩家入口创建资源、取得技能、研究与购买，严禁用这些造景操作补足通关。

Unity现场操作使用 `unity command ... --project-path <absolute project> --caller plugin --skill unity-cli --format json`。确认真实帧推进后，开局Button选择，使用真实OperationPayloadDrag/OperationPointer/Raycast落位，实际结算按钮两次并等待ScoringShow自然结束。累计9时加班界面显示目标41而不是完整50，Stage初始能量仍0。没有通过补数值使此局获胜。

截图在 `Assets/Screenshots/QC-2026-10-12/`。01—05来自实际阶段；06明确为注入六件工具的**UI布局造景**。最后退出Play Mode，所有造景状态丢弃，Main dirty=false。
