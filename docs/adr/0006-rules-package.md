# 离开 Unity 的那一次编译改挂无引擎架构切片

Unity 进程里，规则源码仍继承真正的 QFramework。`dotnet test` 和网页把同一批规则源文件再对着 `kernel/TheCall.QfSubset` 编译一次。切片使用命名空间 `QFramework`，不含场景、时间或引擎随机。ADR 0004 里「规则留在同一个 QFramework 程序集」因此不再成立，因为那份程序集引用 UnityEngine。规则源文件仍然不调用场景、时间或 `UnityEngine.Random`。表现层仍只通过命令、查询和结算记录接触规则。

内容只有 `Assets/StreamingAssets/call-book.json`。Unity 宿主、测试和网页都把这份文本交给 `ContentBook.Parse`。
