/*
 * 怪物运动（网页编辑器用）。和 Unity 里的 MonsterRigKinematics / MonsterMotionController 逐行对应：
 * 公式、常数、更新顺序都必须与 C# 保持一致，否则网页里的手感和游戏里不一样。
 * 骨架来自 MonsterRigSession 的 skeleton 字段；调参来自 motion 字段（MonsterMotionProfile）。
 */
(function (root) {
  "use strict";

  const DEG = Math.PI / 180;
  const STEP_SECONDS = 1 / 120;
  const MAX_FRAME_SECONDS = 0.1;
  const CENTER_CATCH_SECONDS = 0.02;
  const CENTER_CATCH_SNAP = 0.35;
  const BOUNCE = 0.2;
  const STEP_EPSILON = 1e-5;

  const DEFAULT_PROFILE = {
    idleEnabled: true,
    idleCyclesPerSecond: 0.42,
    idleBodyLift: 1.2,
    gripResponse: 16,
    gripDamping: 0.75,
    dragSpeedReference: 360,
    blendInSeconds: 0.08,
    blendOutSeconds: 0.3,
    driverSmoothing: 0.05,
    rootTiltResponse: 9,
    rootTiltDamping: 0.35,
    jointResponse: 11,
    jointDamping: 0.3,
    reactionResponse: 14,
    reactionDamping: 0.5,
  };

  function isGrab(joint) {
    return joint === "Head" || joint === "Hand" || joint === "Foot" || joint === "Tail";
  }

  function phaseOf(joint) {
    switch (joint) {
      case "Hand": return 1;
      case "Foot": return 2;
      case "Tail": return 3.4;
      case "Trim": return 0.5;
      default: return 0;
    }
  }

  /* 关节的响应倍率（x）与阻尼倍率（y）。尾巴更软，手更利落。 */
  function tuningOf(joint) {
    switch (joint) {
      case "Hand": return [1.25, 1];
      case "Foot": return [1.15, 1];
      case "Tail": return [0.75, 0.8];
      default: return [1, 1];
    }
  }

  function createFrame(skeleton) {
    const count = skeleton.bones.length;
    return {
      x: new Float64Array(count),
      y: new Float64Array(count),
      rot: new Float64Array(count),
      mirror: new Array(count).fill(false),
      pinX: new Float64Array(skeleton.pinCount || 0),
      pinY: new Float64Array(skeleton.pinCount || 0),
    };
  }

  /* 前向运动学：子骨头的世界旋转 = 父旋转 + 符号 × 本地角度（父镜像时符号为负）；镜像 = 父镜像 异或 本地镜像。 */
  function solve(skeleton, frame, angles, tilt, pivotX, pivotY, gripX, gripY, lift) {
    const bones = skeleton.bones;
    for (let i = 0; i < bones.length; i++) {
      const bone = bones[i];
      if (bone.parent < 0) {
        const radians = tilt * DEG;
        const cos = Math.cos(radians);
        const sin = Math.sin(radians);
        const dx = bone.mountX - gripX;
        const dy = bone.mountY - gripY;
        frame.x[i] = pivotX + cos * dx - sin * dy;
        frame.y[i] = pivotY + lift + sin * dx + cos * dy;
        frame.rot[i] = tilt;
        frame.mirror[i] = !!bone.localMirror;
      } else {
        const parent = bone.parent;
        const parentRadians = frame.rot[parent] * DEG;
        const parentCos = Math.cos(parentRadians);
        const parentSin = Math.sin(parentRadians);
        const ox = frame.mirror[parent] ? -bone.socketOffsetX : bone.socketOffsetX;
        const oy = bone.socketOffsetY;
        frame.x[i] = frame.x[parent] + parentCos * ox - parentSin * oy;
        frame.y[i] = frame.y[parent] + parentSin * ox + parentCos * oy;
        const sign = frame.mirror[parent] ? -1 : 1;
        frame.rot[i] = frame.rot[parent] + sign * angles[i];
        frame.mirror[i] = frame.mirror[parent] !== !!bone.localMirror;
      }

      const selfRadians = frame.rot[i] * DEG;
      const selfCos = Math.cos(selfRadians);
      const selfSin = Math.sin(selfRadians);
      for (let k = 0; k < bone.sockets.length; k++) {
        const socket = bone.sockets[k];
        let sx = socket.x - bone.mountX;
        const sy = socket.y - bone.mountY;
        if (frame.mirror[i]) sx = -sx;
        frame.pinX[bone.pinStart + k] = frame.x[i] + selfCos * sx - selfSin * sy;
        frame.pinY[bone.pinStart + k] = frame.y[i] + selfSin * sx + selfCos * sy;
      }
    }
  }

  function restFrame(skeleton) {
    const frame = createFrame(skeleton);
    if (skeleton.bones.length > 0) {
      const root = skeleton.bones[0];
      solve(skeleton, frame, new Float64Array(skeleton.bones.length), 0, root.mountX, root.mountY, root.mountX, root.mountY, 0);
    }
    return frame;
  }

  /* 按绘制顺序输出每根骨头的世界姿势；字段名和 Unity 端的 MonsterRigNode 对齐，方便复用绘制代码。 */
  function toNodes(skeleton, frame) {
    return skeleton.drawOrder.map((index) => {
      const bone = skeleton.bones[index];
      return {
        index,
        partId: bone.partId,
        socketId: bone.socketId,
        width: bone.width,
        height: bone.height,
        attachX: bone.mountX,
        attachY: bone.mountY,
        worldAttachX: frame.x[index],
        worldAttachY: frame.y[index],
        worldRotation: frame.rot[index],
        worldMirror: frame.mirror[index],
        kind: bone.kind,
        color: bone.color,
        paletteIndex: bone.paletteIndex,
        order: bone.order,
      };
    });
  }

  function toPins(skeleton, frame) {
    const pins = [];
    for (let i = 0; i < skeleton.bones.length; i++) {
      const bone = skeleton.bones[i];
      for (let k = 0; k < bone.sockets.length; k++) {
        const socket = bone.sockets[k];
        pins.push({
          partId: bone.partId,
          socketId: socket.id,
          group: socket.group,
          accepts: socket.accepts,
          mount: socket.mount,
          worldX: frame.pinX[bone.pinStart + k],
          worldY: frame.pinY[bone.pinStart + k],
        });
      }
    }
    return pins;
  }

  function spring(value, speed, target, h, omega, zeta, limit) {
    const acceleration = omega * omega * (target - value) - 2 * zeta * omega * speed;
    let nextSpeed = speed + acceleration * h;
    let nextValue = value + nextSpeed * h;
    if (nextValue > limit) {
      nextValue = limit;
      if (nextSpeed > 0) nextSpeed = -nextSpeed * BOUNCE;
    } else if (nextValue < -limit) {
      nextValue = -limit;
      if (nextSpeed < 0) nextSpeed = -nextSpeed * BOUNCE;
    }
    return { value: nextValue, speed: nextSpeed };
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  /*
   * 控制器：待机、抓取、计分反应三层叠加在同一副骨架上。
   * 抓住的是身体中心。按下点偏了，偏差在约 0.1 秒内收到零；这段时间指针移动多少，中心就移动多少。
   * 对齐之后中心与指针重合。身体倾斜和四肢仍由拖拽速度的弹簧驱动。松手后中心弹簧回原位。
   * 关节的拖拽角度是硬上限：弹簧过冲时会被夹住并小幅回弹。
   * idleScale 是网页端专用的开关（编辑挂点时传 0 让待机暂停），不影响和游戏一致的动力学。
   */
  function createController(skeleton, profile, seed, startTime) {
    const p = Object.assign({}, DEFAULT_PROFILE, profile || {});
    const bones = skeleton.bones;
    const count = bones.length;
    const angle = new Float64Array(count);
    const speed = new Float64Array(count);
    const omega = new Float64Array(count);
    const zeta = new Float64Array(count);
    const composed = new Float64Array(count);
    const rootInertia = count > 0 ? Math.sqrt(bones[0].inertia) : 1;
    const rootOmega = p.rootTiltResponse / rootInertia;
    const rootZeta = p.rootTiltDamping;
    const gripOmega = p.gripResponse / rootInertia;
    const gripZeta = p.gripDamping;
    const reactionOmega = p.reactionResponse;
    const reactionZeta = p.reactionDamping;
    for (let i = 1; i < count; i++) {
      const bone = bones[i];
      if (!isGrab(bone.joint)) continue;
      const tuning = tuningOf(bone.joint);
      omega[i] = p.jointResponse * tuning[0] / Math.sqrt(bone.inertia);
      zeta[i] = p.jointDamping * tuning[1];
    }

    const s = {
      time: startTime || 0,
      accumulator: 0,
      weight: 0,
      driver: 0,
      holding: false,
      gripX: 0,
      gripY: 0,
      targetX: 0,
      targetY: 0,
      alignX: 0,
      alignY: 0,
      pointerSampleX: 0,
      pointerSampleY: 0,
      pivotX: 0,
      pivotY: 0,
      pivotSpeedX: 0,
      pivotSpeedY: 0,
      tilt: 0,
      tiltSpeed: 0,
      reactHead: 0,
      reactHeadSpeed: 0,
      reactFeet: 0,
      reactFeetSpeed: 0,
      targetHead: 0,
      targetFeet: 0,
    };
    seed = seed || 0;

    function step(h) {
      const blend = s.holding ? p.blendInSeconds : p.blendOutSeconds;
      s.weight += ((s.holding ? 1 : 0) - s.weight) * (1 - Math.exp(-h / Math.max(0.001, blend)));

      const reference = Math.max(1, p.dragSpeedReference);
      const raw = clamp(s.pivotSpeedX / reference, -1, 1) * s.weight;
      s.driver += (raw - s.driver) * (1 - Math.exp(-h / Math.max(0.001, p.driverSmoothing)));

      let next;
      if (!s.holding) {
        next = spring(s.pivotX, s.pivotSpeedX, s.gripX, h, gripOmega, gripZeta, Infinity);
        s.pivotX = next.value;
        s.pivotSpeedX = next.speed;
        next = spring(s.pivotY, s.pivotSpeedY, s.gripY, h, gripOmega, gripZeta, Infinity);
        s.pivotY = next.value;
        s.pivotSpeedY = next.speed;
      }

      if (count > 0) {
        const root = bones[0];
        next = spring(s.tilt, s.tiltSpeed, -root.drag * s.driver, h, rootOmega, rootZeta, Math.max(0, root.drag));
        s.tilt = next.value;
        s.tiltSpeed = next.speed;
      }

      for (let i = 1; i < count; i++) {
        const bone = bones[i];
        if (!isGrab(bone.joint)) continue;
        next = spring(angle[i], speed[i], bone.drag * s.driver, h, omega[i], zeta[i], Math.max(0, bone.drag));
        angle[i] = next.value;
        speed[i] = next.speed;
      }

      next = spring(s.reactHead, s.reactHeadSpeed, s.targetHead, h, reactionOmega, reactionZeta, Infinity);
      s.reactHead = next.value;
      s.reactHeadSpeed = next.speed;
      next = spring(s.reactFeet, s.reactFeetSpeed, s.targetFeet, h, reactionOmega, reactionZeta, Infinity);
      s.reactFeet = next.value;
      s.reactFeetSpeed = next.speed;
    }

    function placeBody() {
      s.pivotX = s.targetX + s.alignX;
      s.pivotY = s.targetY + s.alignY;
    }

    function catchUp(dt) {
      const decay = Math.exp(-dt / CENTER_CATCH_SECONDS);
      s.alignX *= decay;
      s.alignY *= decay;
      if (s.alignX * s.alignX + s.alignY * s.alignY <= CENTER_CATCH_SNAP * CENTER_CATCH_SNAP) {
        s.alignX = 0;
        s.alignY = 0;
      }
      placeBody();
      s.pivotSpeedX = (s.targetX - s.pointerSampleX) / dt;
      s.pivotSpeedY = (s.targetY - s.pointerSampleY) / dt;
      s.pointerSampleX = s.targetX;
      s.pointerSampleY = s.targetY;
    }

    function advance(delta) {
      if (count === 0) return;
      const dt = clamp(delta, 0, MAX_FRAME_SECONDS);
      s.time += dt;
      if (s.holding && dt > 0) catchUp(dt);
      s.accumulator += dt;
      while (s.accumulator >= STEP_SECONDS - STEP_EPSILON) {
        step(STEP_SECONDS);
        s.accumulator -= STEP_SECONDS;
      }
    }

    function compose(frame, idleScale) {
      if (count === 0) return;
      const scale = idleScale === undefined ? 1 : idleScale;
      const idle = p.idleEnabled ? (1 - s.weight) * scale : 0;
      const cps = p.idleCyclesPerSecond;
      const clock = s.time + seed;
      for (let i = 0; i < count; i++) {
        const bone = bones[i];
        if (bone.joint === "Root") {
          composed[i] = 0;
          continue;
        }
        const wave = Math.sin(2 * Math.PI * cps * clock + phaseOf(bone.joint));
        let value = idle * bone.swing * wave;
        if (isGrab(bone.joint)) value += angle[i];
        if (bone.kind === "Head") value += s.reactHead;
        if (bone.kind === "Foot") value += s.reactFeet;
        composed[i] = value;
      }

      const root = bones[0];
      const slow = Math.sin(Math.PI * cps * (s.time + 0.7 * seed));
      const fast = Math.sin(2 * Math.PI * cps * clock);
      solve(
        skeleton,
        frame,
        composed,
        s.tilt + idle * root.swing * slow,
        root.mountX + s.pivotX,
        root.mountY + s.pivotY,
        root.mountX + s.gripX,
        root.mountY + s.gripY,
        idle * p.idleBodyLift * fast,
      );
    }

    /* 抓住身体中心。这一下不挪怪物，偏差随后收到零。 */
    function grab(x, y) {
      s.alignX = s.pivotX - x;
      s.alignY = s.pivotY - y;
      s.targetX = x;
      s.targetY = y;
      s.pointerSampleX = x;
      s.pointerSampleY = y;
      s.holding = true;
      placeBody();
    }

    function hold(x, y) {
      if (!s.holding) return;
      s.targetX = x;
      s.targetY = y;
      placeBody();
    }

    function release() {
      s.holding = false;
    }

    function setReaction(head, feet) {
      s.targetHead = head;
      s.targetFeet = feet;
    }

    function settled() {
      if (s.holding) return false;
      if (Math.abs(s.pivotX - s.gripX) > 0.02 || Math.abs(s.pivotY - s.gripY) > 0.02) return false;
      if (Math.abs(s.pivotSpeedX) > 0.5 || Math.abs(s.pivotSpeedY) > 0.5) return false;
      if (Math.abs(s.tilt) > 0.02 || Math.abs(s.tiltSpeed) > 0.5) return false;
      if (Math.abs(s.reactHead) > 0.02 || Math.abs(s.reactFeet) > 0.02) return false;
      for (let i = 1; i < count; i++) {
        if (!isGrab(bones[i].joint)) continue;
        if (Math.abs(angle[i]) > 0.02 || Math.abs(speed[i]) > 0.5) return false;
      }
      return true;
    }

    return {
      advance,
      compose,
      grab,
      hold,
      release,
      setReaction,
      settled,
      holding: () => s.holding,
      weight: () => s.weight,
      clock: () => s.time,
    };
  }

  const api = {
    STEP_SECONDS,
    MAX_FRAME_SECONDS,
    createFrame,
    solve,
    restFrame,
    toNodes,
    toPins,
    createController,
    isGrab,
    phaseOf,
    tuningOf,
  };

  if (typeof module === "object" && module.exports) module.exports = api;
  root.MonsterMotion = api;
})(typeof window !== "undefined" ? window : globalThis);
