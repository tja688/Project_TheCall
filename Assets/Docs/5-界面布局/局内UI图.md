# 局内界面（操作阶段）

本图保留策划布局参考。对齐后，操作阶段主按钮应显示「开始结算／加班结算」，商店退出显示「下一天／结束本局」。加班仍展示完整需求与累计当前能量，差额单独补充；顶栏不得隐藏超额门槛。实际交付与资产绑定见[已确认落地规格](../../../docs/specs/new-planning-playable-migration.md)。

<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 680 460" width="680" role="img" style="max-width:100%;height:auto;font-family:'Segoe UI','Microsoft YaHei',sans-serif">
<title>局内主界面布局（操作阶段）</title>
<desc>顶部状态栏显示天数、需求能量、当前能量、超额能量与累计金币；左区上行为培育槽、下行为提取槽（编号 1 至 5，内摆工具摆件）、提取槽下方为废弃槽；右列为怪物笼与基因槽；右下角为「下一天」。</desc>

<rect x="40" y="40" width="600" height="36" rx="8" fill="#F7F6F2" stroke="#C9C7BE" stroke-width="0.5"/>
<text x="54" y="63" font-size="14" font-weight="500" fill="#2C2C2A">第 03 天</text>
<line x1="130" y1="48" x2="130" y2="68" stroke="#D8D6CD" stroke-width="0.5"/>
<text x="142" y="63" font-size="12" fill="#5F5E5A">需求 100</text>
<text x="222" y="63" font-size="12" fill="#5F5E5A">当前 —</text>
<text x="296" y="63" font-size="12" fill="#5F5E5A">超额 120</text>
<line x1="380" y1="48" x2="380" y2="68" stroke="#D8D6CD" stroke-width="0.5"/>
<text x="392" y="63" font-size="12" fill="#5F5E5A">金币 40</text>
<text x="524" y="63" font-size="12" fill="#5F5E5A">科技</text>
<text x="586" y="63" font-size="12" fill="#5F5E5A">设置</text>

<rect x="40" y="84" width="392" height="106" rx="8" fill="#F7F6F2" stroke="#C9C7BE" stroke-width="0.5"/>
<text x="52" y="101" font-size="12" fill="#5F5E5A">培育槽</text>
<text x="102" y="101" font-size="11" fill="#888780">（≥2 只满足育种条件）</text>

<rect x="49" y="110" width="70" height="70" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="72" y="113" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="76" cy="152" rx="15" ry="12" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="71" cy="149" r="1.8" fill="#2C2C2A"/>
<circle cx="81" cy="149" r="1.8" fill="#2C2C2A"/>
<rect x="100" y="132" width="14" height="24" rx="4" fill="#CECBF6" stroke="#7F77DD" stroke-width="0.5"/>

<rect x="125" y="110" width="70" height="70" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="148" y="113" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="152" cy="152" rx="15" ry="12" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="147" cy="149" r="1.8" fill="#2C2C2A"/>
<circle cx="157" cy="149" r="1.8" fill="#2C2C2A"/>

<rect x="201" y="110" width="70" height="70" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="224" y="113" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="228" cy="152" rx="15" ry="12" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="223" cy="149" r="1.8" fill="#2C2C2A"/>
<circle cx="233" cy="149" r="1.8" fill="#2C2C2A"/>
<rect x="252" y="132" width="14" height="24" rx="4" fill="#CECBF6" stroke="#7F77DD" stroke-width="0.5"/>

<rect x="277" y="110" width="70" height="70" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="300" y="113" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="304" cy="152" rx="15" ry="12" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="299" cy="149" r="1.8" fill="#2C2C2A"/>
<circle cx="309" cy="149" r="1.8" fill="#2C2C2A"/>

<rect x="353" y="110" width="70" height="70" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="376" y="113" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="380" cy="152" rx="15" ry="12" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="375" cy="149" r="1.8" fill="#2C2C2A"/>
<circle cx="385" cy="149" r="1.8" fill="#2C2C2A"/>
<rect x="404" y="132" width="14" height="24" rx="4" fill="#CECBF6" stroke="#7F77DD" stroke-width="0.5"/>

<rect x="40" y="198" width="392" height="134" rx="8" fill="#F7F6F2" stroke="#C9C7BE" stroke-width="0.5"/>
<text x="52" y="215" font-size="12" fill="#5F5E5A">提取槽</text>
<text x="102" y="215" font-size="11" fill="#888780">（编号＝结算顺序；摆件＝工具，不占格）</text>

<rect x="49" y="224" width="70" height="78" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="72" y="227" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="84" cy="266" rx="16" ry="13" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="78" cy="263" r="1.8" fill="#2C2C2A"/>
<circle cx="90" cy="263" r="1.8" fill="#2C2C2A"/>
<circle cx="106" cy="234" r="5" fill="#9FE1CB" stroke="#1D9E75" stroke-width="0.5"/>
<text x="84" y="319" text-anchor="middle" font-size="12" fill="#888780">1</text>

<rect x="125" y="224" width="70" height="78" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="148" y="227" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="160" cy="266" rx="16" ry="13" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="154" cy="263" r="1.8" fill="#2C2C2A"/>
<circle cx="166" cy="263" r="1.8" fill="#2C2C2A"/>
<text x="160" y="319" text-anchor="middle" font-size="12" fill="#888780">2</text>

<rect x="201" y="224" width="70" height="78" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="224" y="227" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="236" cy="266" rx="16" ry="13" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="230" cy="263" r="1.8" fill="#2C2C2A"/>
<circle cx="242" cy="263" r="1.8" fill="#2C2C2A"/>
<circle cx="258" cy="234" r="5" fill="#9FE1CB" stroke="#1D9E75" stroke-width="0.5"/>
<text x="236" y="319" text-anchor="middle" font-size="12" fill="#888780">3</text>

<rect x="277" y="224" width="70" height="78" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="300" y="227" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="312" cy="266" rx="16" ry="13" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="306" cy="263" r="1.8" fill="#2C2C2A"/>
<circle cx="318" cy="263" r="1.8" fill="#2C2C2A"/>
<text x="312" y="319" text-anchor="middle" font-size="12" fill="#888780">4</text>

<rect x="353" y="224" width="70" height="78" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="376" y="227" width="24" height="6" rx="2" fill="#E8E6DE"/>
<ellipse cx="388" cy="266" rx="16" ry="13" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<circle cx="382" cy="263" r="1.8" fill="#2C2C2A"/>
<circle cx="394" cy="263" r="1.8" fill="#2C2C2A"/>
<circle cx="410" cy="234" r="5" fill="#9FE1CB" stroke="#1D9E75" stroke-width="0.5"/>
<text x="388" y="319" text-anchor="middle" font-size="12" fill="#888780">5</text>

<rect x="40" y="340" width="392" height="52" rx="8" fill="#FCEBEB" stroke="#E24B4A" stroke-width="0.5"/>
<text x="52" y="371" font-size="12" fill="#791F1F">废弃槽</text>
<rect x="104" y="356" width="22" height="20" rx="3" fill="#F7C1C1" stroke="#A32D2D" stroke-width="0.5"/>
<line x1="101" y1="356" x2="129" y2="356" stroke="#A32D2D" stroke-width="0.5"/>
<text x="138" y="371" font-size="11" fill="#A32D2D">拖入怪物消灭，取一个基因</text>

<rect x="440" y="84" width="200" height="156" rx="8" fill="#F7F6F2" stroke="#C9C7BE" stroke-width="0.5"/>
<text x="452" y="100" font-size="12" fill="#5F5E5A">怪物笼</text>
<rect x="448" y="110" width="56" height="38" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<ellipse cx="476" cy="129" rx="12" ry="9" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<rect x="512" y="110" width="56" height="38" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<ellipse cx="540" cy="129" rx="12" ry="9" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<rect x="576" y="110" width="56" height="38" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<ellipse cx="604" cy="129" rx="12" ry="9" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<rect x="448" y="152" width="56" height="38" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<ellipse cx="476" cy="171" rx="12" ry="9" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<rect x="512" y="152" width="56" height="38" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<ellipse cx="540" cy="171" rx="12" ry="9" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<rect x="576" y="152" width="56" height="38" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="448" y="194" width="56" height="38" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<ellipse cx="476" cy="213" rx="12" ry="9" fill="#FAC775" stroke="#BA7517" stroke-width="0.5"/>
<rect x="512" y="194" width="56" height="38" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="576" y="194" width="56" height="38" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>

<rect x="440" y="248" width="200" height="144" rx="8" fill="#F7F6F2" stroke="#C9C7BE" stroke-width="0.5"/>
<text x="452" y="266" font-size="12" fill="#5F5E5A">基因槽</text>
<circle cx="622" cy="261" r="10" fill="#FFFFFF" stroke="#7F77DD" stroke-width="0.5"/>
<text x="622" y="266" text-anchor="middle" font-size="13" fill="#7F77DD">+</text>
<rect x="448" y="276" width="56" height="32" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="470" y="283" width="12" height="18" rx="3" fill="#CECBF6" stroke="#7F77DD" stroke-width="0.5"/>
<rect x="512" y="276" width="56" height="32" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="534" y="283" width="12" height="18" rx="3" fill="#CECBF6" stroke="#7F77DD" stroke-width="0.5"/>
<rect x="576" y="276" width="56" height="32" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="448" y="316" width="56" height="32" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="470" y="323" width="12" height="18" rx="3" fill="#CECBF6" stroke="#7F77DD" stroke-width="0.5"/>
<rect x="512" y="316" width="56" height="32" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="576" y="316" width="56" height="32" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="448" y="356" width="56" height="32" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="512" y="356" width="56" height="32" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>
<rect x="576" y="356" width="56" height="32" rx="6" fill="#FFFFFF" stroke="#D8D6CD" stroke-width="0.5"/>

<rect x="440" y="400" width="200" height="40" rx="8" fill="#EAF3DE" stroke="#639922" stroke-width="0.5"/>
<text x="540" y="425" text-anchor="middle" font-size="14" font-weight="500" fill="#27500A">下一天</text>
</svg>

| 区域    | 说明                                           |
| ----- | -------------------------------------------- |
| 顶部状态栏 | 第 N 天 ｜ 需求能量 / 当前能量 / 超额能量 ｜ 金币（累计）｜ 科技 ｜ 设置 |
| 培育槽   | 放入怪物培育子代，至少两只即满足育种条件；罐子表示培育中                 |
| 提取槽   | 5 格，编号即结算顺序；槽内小圆点＝工具摆件（不占格、无上限）              |
| 废弃槽   | 位于提取槽下方；拖入怪物**消灭**，取一个基因                     |
| 怪物笼   | 存放玩家拥有的怪物                                    |
| 基因槽   | 存放待植入的基因，右上角「+」扩容                            |
| 主操作按钮 | 操作阶段开始／加班结算；商店退出下一天／结束本局，不跳过商店选择       |
