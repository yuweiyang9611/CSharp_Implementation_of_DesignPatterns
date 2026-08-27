const repositoryRoot = "https://github.com/yuweiyang9611/CSharp_Implementation_of_DesignPatterns/blob/main/";

const categoryMeta = {
  Creational: { label: "创建型", short: "C", color: "amber" },
  Structural: { label: "结构型", short: "S", color: "coral" },
  Behavioral: { label: "行为型", short: "B", color: "teal" },
};

const patterns = [
  {
    key: "iterator", english: "Iterator", chinese: "迭代器", category: "Behavioral",
    intent: "在不暴露集合内部结构的前提下顺序访问元素。",
    scenario: "播放列表按曲序遍历，并用另一种迭代方式筛选收藏曲目。",
    source: "src/DesignPatterns/Behavioral/IteratorDemo.cs",
    practice: "examples/DocumentWorkflow/Domain/SectionCollection.cs",
    guide: "#31-第-1-章-iterator迭代器一个一个遍历",
  },
  {
    key: "adapter", english: "Adapter", chinese: "适配器", category: "Structural",
    intent: "把已有类型的接口转换为客户端期望的接口，让原本不兼容的类型协同工作。",
    scenario: "把旧式华氏温度设备适配为新系统使用的摄氏温度传感器。",
    source: "src/DesignPatterns/Structural/AdapterDemo.cs",
    practice: "examples/SmartHome/Patterns/Structural/LegacyAirConditionerAdapter.cs",
    guide: "#32-第-2-章-adapter适配器填平接口与单位差异",
  },
  {
    key: "template-method", english: "Template Method", chinese: "模板方法", category: "Behavioral",
    intent: "定义算法骨架，并允许子类改写其中的特定步骤。",
    scenario: "统一 CSV 与 JSON 订单导出的骨架，只让格式相关步骤发生变化。",
    source: "src/DesignPatterns/Behavioral/TemplateMethodDemo.cs",
    practice: "examples/DocumentWorkflow/Pipeline/PublishingPipeline.cs",
    guide: "#41-第-3-章-template-method模板方法父类守住流程",
  },
  {
    key: "factory-method", english: "Factory Method", chinese: "工厂方法", category: "Creational",
    intent: "定义创建产品的接口，把具体产品的选择延迟到子类。",
    scenario: "公路与航空运输工作流分别创建合适的承运商。",
    source: "src/DesignPatterns/Creational/FactoryMethodDemo.cs",
    practice: "examples/OnlineStore/Payments/PaymentProcessorCreator.cs",
    guide: "#42-第-4-章-factory-method工厂方法把创建哪一种留给子类",
  },
  {
    key: "singleton", english: "Singleton", chinese: "单例", category: "Creational",
    intent: "保证一个类型只有一个实例，并提供统一的全局访问入口。",
    scenario: "用线程安全的惰性实例提供不可变应用设置。",
    source: "src/DesignPatterns/Creational/SingletonDemo.cs",
    practice: "examples/SmartHome/Patterns/Creational/DeviceTypeRegistry.cs",
    guide: "#51-第-5-章-singleton单例先问生命周期再写全局入口",
  },
  {
    key: "prototype", english: "Prototype", chinese: "原型", category: "Creational",
    intent: "通过复制现有原型创建对象，复用昂贵或复杂的初始配置。",
    scenario: "复制营销活动模板，并验证可变渠道集合已经深复制。",
    source: "src/DesignPatterns/Creational/PrototypeDemo.cs",
    practice: "examples/DocumentWorkflow/Domain/ReportDocument.cs",
    guide: "#52-第-6-章-prototype原型复制的是值还是共享引用",
  },
  {
    key: "builder", english: "Builder", chinese: "建造者", category: "Creational",
    intent: "把复杂对象的分步构建与最终表示分离，使同一过程能创建不同配置。",
    scenario: "用流畅步骤创建通过校验的预览版与生产版部署计划。",
    source: "src/DesignPatterns/Creational/BuilderDemo.cs",
    practice: "examples/OnlineStore/Building/OrderBuilder.cs",
    guide: "#53-第-7-章-builder建造者让复杂对象只能以合法方式完成",
  },
  {
    key: "abstract-factory", english: "Abstract Factory", chinese: "抽象工厂", category: "Creational",
    intent: "创建一组相互兼容的产品，而无需让客户端依赖具体产品类型。",
    scenario: "一次切换 Windows 与移动端的按钮、输入框等兼容控件族。",
    source: "src/DesignPatterns/Creational/AbstractFactoryDemo.cs",
    practice: "examples/DocumentWorkflow/Output/OutputComponentFactories.cs",
    guide: "#54-第-8-章-abstract-factory抽象工厂一次选择一整套兼容产品",
  },
  {
    key: "bridge", english: "Bridge", chinese: "桥接", category: "Structural",
    intent: "把抽象层次与实现层次分离，使二者可以独立扩展并自由组合。",
    scenario: "让告警类型与 Email、SMS 发送渠道独立扩展和自由组合。",
    source: "src/DesignPatterns/Structural/BridgeDemo.cs",
    practice: "examples/SmartHome/Patterns/Structural/BridgeDevices.cs",
    guide: "#61-第-9-章-bridge桥接不要为每种组合建立子类",
  },
  {
    key: "strategy", english: "Strategy", chinese: "策略", category: "Behavioral",
    intent: "定义可互换的算法，并让客户端在运行时选择。",
    scenario: "在标准、加急和自提柜配送报价之间动态切换。",
    source: "src/DesignPatterns/Behavioral/StrategyDemo.cs",
    practice: "examples/OnlineStore/Pricing/PricingStrategies.cs",
    guide: "#62-第-10-章-strategy策略整体替换算法",
  },
  {
    key: "composite", english: "Composite", chinese: "组合", category: "Structural",
    intent: "把对象组织成树形结构，使客户端能一致地处理叶节点和组合节点。",
    scenario: "工作分解树用同一接口表示任务和任务组，并自动汇总工时。",
    source: "src/DesignPatterns/Structural/CompositeDemo.cs",
    practice: "examples/SmartHome/Patterns/Structural/HomeComposite.cs",
    guide: "#71-第-11-章-composite组合让叶子和容器共享协议",
  },
  {
    key: "decorator", english: "Decorator", chinese: "装饰器", category: "Structural",
    intent: "在不修改原对象的前提下，通过包装动态叠加职责。",
    scenario: "在房价上依次叠加会员折扣、服务费和税，观察顺序影响。",
    source: "src/DesignPatterns/Structural/DecoratorDemo.cs",
    practice: "examples/DocumentWorkflow/Output/ArtifactDecorators.cs",
    guide: "#72-第-12-章-decorator装饰器按顺序叠加职责",
  },
  {
    key: "visitor", english: "Visitor", chinese: "访问者", category: "Behavioral",
    intent: "在不修改元素类型的前提下，为对象结构增加新操作。",
    scenario: "同一购物车元素结构分别接受计价与运费 Visitor。",
    source: "src/DesignPatterns/Behavioral/VisitorDemo.cs",
    practice: "examples/DocumentWorkflow/Analysis/ReportStatisticsVisitor.cs",
    guide: "#81-第-13-章-visitor访问者数据类型稳定操作经常增加",
  },
  {
    key: "chain-of-responsibility", english: "Chain of Responsibility", chinese: "职责链", category: "Behavioral",
    intent: "将请求沿处理者链传递，直到某个处理者能够处理它。",
    scenario: "不同金额的费用申请沿团队负责人、经理和财务总监逐级审批。",
    source: "src/DesignPatterns/Behavioral/ChainOfResponsibilityDemo.cs",
    practice: "examples/OnlineStore/Validation/CheckoutValidationChain.cs",
    guide: "#82-第-14-章-chain-of-responsibility责任链请求沿链传递",
  },
  {
    key: "facade", english: "Facade", chinese: "外观", category: "Structural",
    intent: "为复杂子系统提供一个更简单、面向用例的统一入口。",
    scenario: "用一个结账入口统一编排库存、支付和配送子系统。",
    source: "src/DesignPatterns/Structural/FacadeDemo.cs",
    practice: "examples/OnlineStore/Application/CheckoutFacade.cs",
    guide: "#91-第-15-章-facade外观暴露一个用例级窗口",
  },
  {
    key: "mediator", english: "Mediator", chinese: "中介者", category: "Behavioral",
    intent: "用中介者集中协调对象交互，降低对象之间的直接耦合。",
    scenario: "控制塔集中管理飞机降落队列，飞机之间无需直接通信。",
    source: "src/DesignPatterns/Behavioral/MediatorDemo.cs",
    practice: "examples/SmartHome/Patterns/Behavioral/HomeMediator.cs",
    guide: "#92-第-16-章-mediator中介者同事只认识协调者",
  },
  {
    key: "observer", english: "Observer", chinese: "观察者", category: "Behavioral",
    intent: "当主题状态变化时，自动通知所有已订阅的观察者。",
    scenario: "订单事件驱动邮件与审计订阅者，并演示安全退订。",
    source: "src/DesignPatterns/Behavioral/ObserverDemo.cs",
    practice: "examples/OnlineStore/Events/OrderEventPublisher.cs",
    guide: "#101-第-17-章-observer观察者状态变化后通知订阅者",
  },
  {
    key: "memento", english: "Memento", chinese: "备忘录", category: "Behavioral",
    intent: "在不暴露内部实现的前提下捕获并恢复对象状态。",
    scenario: "文本编辑器保存两级快照，并在不泄露字段的情况下恢复。",
    source: "src/DesignPatterns/Behavioral/MementoDemo.cs",
    practice: "examples/SmartHome/Patterns/Behavioral/SafetySceneMemento.cs",
    guide: "#102-第-18-章-memento备忘录保存状态但不泄露内部字段",
  },
  {
    key: "state", english: "State", chinese: "状态", category: "Behavioral",
    intent: "让对象在内部状态改变时切换其行为。",
    scenario: "订单在待支付、已支付、已发货和已取消状态间保护合法转换。",
    source: "src/DesignPatterns/Behavioral/StateDemo.cs",
    practice: "examples/OnlineStore/States/OrderStates.cs",
    guide: "#103-第-19-章-state状态让当前状态对象决定行为",
  },
  {
    key: "flyweight", english: "Flyweight", chinese: "享元", category: "Structural",
    intent: "共享细粒度对象的内在状态，以较小内存代价表示大量对象。",
    scenario: "多个地图标记复用不可变的咖啡店样式对象。",
    source: "src/DesignPatterns/Structural/FlyweightDemo.cs",
    practice: "examples/DocumentWorkflow/Domain/StyleFlyweightFactory.cs",
    guide: "#111-第-20-章-flyweight享元把内在状态和外在状态分开",
  },
  {
    key: "proxy", english: "Proxy", chinese: "代理", category: "Structural",
    intent: "为另一个对象提供替身，以控制访问并附加延迟加载、缓存或权限检查。",
    scenario: "商品目录代理缓存远程查询，在客户端不变的情况下减少调用。",
    source: "src/DesignPatterns/Structural/ProxyDemo.cs",
    practice: "examples/SmartHome/Patterns/Structural/AuthorizedDeviceProxy.cs",
    guide: "#112-第-21-章-proxy代理客户端不变访问路径受控",
  },
  {
    key: "command", english: "Command", chinese: "命令", category: "Behavioral",
    intent: "把请求封装为对象，从而支持操作历史、撤销与重做。",
    scenario: "把文本追加和替换变成对象，为编辑器提供 Undo / Redo。",
    source: "src/DesignPatterns/Behavioral/CommandDemo.cs",
    practice: "examples/SmartHome/Patterns/Behavioral/Commands.cs",
    guide: "#121-第-22-章-command命令让一次请求拥有历史",
  },
  {
    key: "interpreter", english: "Interpreter", chinese: "解释器", category: "Behavioral",
    intent: "为简单语言建立语法表示，并解释该语言中的表达式。",
    scenario: "用表达式树解释由角色、部门和启用状态组成的访问规则。",
    source: "src/DesignPatterns/Behavioral/InterpreterDemo.cs",
    practice: "examples/DocumentWorkflow/Filtering/SectionExpressions.cs",
    guide: "#122-第-23-章-interpreter解释器用对象树表达小语言",
  },
].map((pattern, index) => ({ ...pattern, number: index + 1 }));

const elements = {
  grid: document.querySelector("#pattern-grid"),
  search: document.querySelector("#pattern-search"),
  result: document.querySelector("#pattern-result"),
  noResults: document.querySelector("#no-results"),
  filters: [...document.querySelectorAll(".filter-button")],
  progressCount: document.querySelector("#progress-count"),
  progressTrack: document.querySelector("#progress-track"),
  progressBar: document.querySelector("#progress-bar"),
  resetProgress: document.querySelector("#reset-progress"),
  dialog: document.querySelector("#pattern-dialog"),
};

const storageKey = "csharp-design-patterns-progress-v1";
let activeCategory = "all";
let searchQuery = "";
let learned = loadProgress();

function loadProgress() {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey) ?? "[]");
    return new Set(saved.filter((key) => patterns.some((pattern) => pattern.key === key)));
  } catch {
    return new Set();
  }
}

function saveProgress() {
  try {
    localStorage.setItem(storageKey, JSON.stringify([...learned]));
  } catch {
    // Progress remains available for this page view when storage is unavailable.
  }
}

function patternCard(pattern) {
  const category = categoryMeta[pattern.category];
  const isLearned = learned.has(pattern.key);
  const paddedNumber = String(pattern.number).padStart(2, "0");

  return `
    <article class="pattern-card ${category.color}${isLearned ? " learned" : ""}">
      <div class="pattern-card-top">
        <span class="pattern-number">${paddedNumber}</span>
        <span class="category-badge"><i>${category.short}</i>${category.label}</span>
      </div>
      <div class="pattern-body">
        <h3>${pattern.english}</h3><p class="pattern-chinese">${pattern.chinese}模式</p>
        <p class="pattern-intent">${pattern.intent}</p>
        <button class="pattern-open" type="button" data-open-pattern="${pattern.key}" aria-label="查看 ${pattern.english} ${pattern.chinese}详情">查看详情 <span aria-hidden="true">→</span></button>
      </div>
      <div class="pattern-card-footer">
        <code>${pattern.key}</code>
        <button class="learn-toggle" type="button" data-learn-pattern="${pattern.key}" aria-pressed="${isLearned}"><span>${isLearned ? "✓" : "+"}</span>${isLearned ? "已学习" : "标记学习"}</button>
      </div>
    </article>`;
}

function renderPatterns() {
  const normalizedQuery = searchQuery.trim().toLocaleLowerCase("zh-CN");
  const visible = patterns.filter((pattern) => {
    const categoryMatches = activeCategory === "all" || pattern.category === activeCategory;
    const searchable = `${pattern.english} ${pattern.chinese} ${pattern.key} ${pattern.intent} ${pattern.scenario} ${categoryMeta[pattern.category].label}`.toLocaleLowerCase("zh-CN");
    return categoryMatches && (!normalizedQuery || searchable.includes(normalizedQuery));
  });

  elements.grid.innerHTML = visible.map(patternCard).join("");
  elements.noResults.hidden = visible.length > 0;
  const categoryText = activeCategory === "all" ? "全部" : categoryMeta[activeCategory].label;
  elements.result.textContent = searchQuery
    ? `${categoryText}中找到 ${visible.length} 个匹配模式`
    : `按初学者顺序显示${categoryText === "全部" ? "全部 " : ""}${visible.length} 种模式`;
  updateProgress();
}

function updateProgress() {
  const completed = learned.size;
  elements.progressCount.textContent = `${completed} / ${patterns.length}`;
  elements.progressTrack.setAttribute("aria-valuenow", String(completed));
  elements.progressBar.style.width = `${(completed / patterns.length) * 100}%`;
}

function openPattern(key) {
  const pattern = patterns.find((item) => item.key === key);
  if (!pattern) return;

  const category = categoryMeta[pattern.category];
  document.querySelector("#dialog-category").textContent = `${category.short} · ${category.label}`;
  document.querySelector("#dialog-number").textContent = String(pattern.number).padStart(2, "0");
  document.querySelector("#dialog-title").textContent = `${pattern.english} / ${pattern.chinese}`;
  document.querySelector("#dialog-intent").textContent = pattern.intent;
  document.querySelector("#dialog-scenario").textContent = pattern.scenario;
  document.querySelector("#dialog-command").textContent = `dotnet run --project src/DesignPatterns.Runner -- ${pattern.key}`;
  document.querySelector("#dialog-guide").href = `guides/fundamentals.html${pattern.guide}`;
  document.querySelector("#dialog-source").href = repositoryRoot + pattern.source;
  document.querySelector("#dialog-practice").href = repositoryRoot + pattern.practice;
  elements.dialog.dataset.pattern = pattern.key;

  const url = new URL(window.location.href);
  url.searchParams.set("pattern", pattern.key);
  history.replaceState(null, "", url);
  if (!elements.dialog.open) elements.dialog.showModal();
}

function closePattern() {
  if (elements.dialog.open) elements.dialog.close();
  const url = new URL(window.location.href);
  url.searchParams.delete("pattern");
  history.replaceState(null, "", `${url.pathname}${url.search}${url.hash}`);
}

async function copyText(text, button) {
  try {
    await navigator.clipboard.writeText(text);
    const original = button.textContent;
    button.textContent = "已复制 ✓";
    window.setTimeout(() => { button.textContent = original; }, 1500);
  } catch {
    button.textContent = "请手动复制";
  }
}

elements.filters.forEach((button) => {
  button.addEventListener("click", () => {
    activeCategory = button.dataset.category;
    elements.filters.forEach((candidate) => {
      const selected = candidate === button;
      candidate.classList.toggle("active", selected);
      candidate.setAttribute("aria-pressed", String(selected));
    });
    renderPatterns();
  });
});

elements.search.addEventListener("input", (event) => {
  searchQuery = event.target.value;
  renderPatterns();
});

elements.grid.addEventListener("click", (event) => {
  const learnButton = event.target.closest("[data-learn-pattern]");
  if (learnButton) {
    const key = learnButton.dataset.learnPattern;
    learned.has(key) ? learned.delete(key) : learned.add(key);
    saveProgress();
    renderPatterns();
    window.requestAnimationFrame(() => {
      elements.grid.querySelector(`[data-learn-pattern="${key}"]`)?.focus();
    });
    return;
  }

  const openButton = event.target.closest("[data-open-pattern]");
  if (openButton) openPattern(openButton.dataset.openPattern);
});

elements.resetProgress.addEventListener("click", () => {
  if (learned.size === 0) return;
  learned = new Set();
  saveProgress();
  renderPatterns();
});

document.querySelector("#dialog-close").addEventListener("click", closePattern);
elements.dialog.addEventListener("click", (event) => {
  const bounds = elements.dialog.getBoundingClientRect();
  const outside = event.clientX < bounds.left || event.clientX > bounds.right ||
    event.clientY < bounds.top || event.clientY > bounds.bottom;
  if (outside) closePattern();
});
elements.dialog.addEventListener("cancel", (event) => {
  event.preventDefault();
  closePattern();
});
document.querySelector("#dialog-copy").addEventListener("click", (event) => {
  copyText(document.querySelector("#dialog-command").textContent, event.currentTarget);
});

document.addEventListener("click", (event) => {
  const button = event.target.closest("[data-copy-command]");
  if (button) copyText(button.dataset.copyCommand, button);
});

renderPatterns();
const requestedPattern = new URL(window.location.href).searchParams.get("pattern");
if (requestedPattern) openPattern(requestedPattern);
