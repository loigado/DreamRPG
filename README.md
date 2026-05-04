<div align="center">

# ⚔️ DreamRPG (In Development)
**A Modern Action RPG Built with Unity**

[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?style=for-the-badge&logo=unity)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-Programming-blue?style=for-the-badge&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Status](https://img.shields.io/badge/Status-In%20Active%20Development-orange?style=for-the-badge)](#)

*DreamRPG is an ambitious 3D Action RPG project designed to showcase advanced gameplay programming, AAA-standard combat mechanics, and scalable software architecture in Unity.*

</div>

---

## 🌟 About The Project

**DreamRPG** is currently in active development. The core focus of this project is to build a robust, scalable, and highly modular foundation for a modern Action RPG (similar to *Elden Ring* or *God of War*). It heavily utilizes the **State Pattern** and **Data-Driven Design** to ensure that adding new weapons, enemies, and skills is seamless and bug-free.

This project serves as a technical showcase of my ability to implement complex gameplay systems, optimize animation pipelines, and write clean, maintainable C# code.

## ✨ Core Features & Technical Highlights

### 🛡️ 1. AAA-Standard Combat System
- **Advanced State Machine:** The entire player controller is built on a hierarchical State Machine (`PlayerStateMachine`, `PlayerBaseState`, etc.), separating movement, combat, aiming, and hit reactions into distinct, manageable modules.
- **Code-Driven Animation (No-Arrows System):** Bypassed Unity's messy Animator transition arrows. The system uses `CrossFadeInFixedTime` and Blend Trees strictly managed by C# logic to ensure frame-perfect responsiveness and zero transition bugs.
- **Dynamic 4-Directional Strafing:** Seamless integration of 2D Blend Trees with Camera-relative and Target-relative movement. Includes "Turn-In-Place" logic to prevent foot-sliding without needing root-motion animations.
- **Block & Perfect Parry:** Built-in stamina management, guard breaks, damage reduction, and a strict time window for perfect parries, complete with visual/audio feedback.

### 🎯 2. Intelligent Targeting & Procedural IK
- **Hard-Lock System:** A sophisticated camera locking mechanism that seamlessly integrates with Cinemachine. It forces the player model to track the target while maintaining smooth strafing mechanics.
- **Procedural Spine Bending (LateUpdate IK):** Custom code to mathematically bend the player's spine (`SpineBone.rotation`) to align with the camera pitch during aiming or to lock the shield forward during blocking, overriding animation anomalies.
- **Dynamic Foot Placement:** Integrates custom Foot IK to ensure the character's feet adapt naturally to uneven terrain and stairs.

### ⚔️ 3. Data-Driven Weapon & Skill Architecture
- **Scriptable Objects (`WeaponData` & `SkillData`):** All weapons (Shortswords, Greatswords, Axes, Bows) are entirely data-driven. Swapping a weapon dynamically updates its damage, stamina cost, associated animation state names, and elemental VFX (e.g., Ice Axe vs. Lightning Sword) without altering the core state machine.
- **Elemental Skill System:** Modular skill states (e.g., `PlayerIceAxeSlamState`, `PlayerLightningDomainState`) that utilize custom hitboxes, particle system instantiation (like the Kratos-style energy shield), and status effect application.

---

## 🏗️ Architecture Overview

The codebase is strictly organized to maintain high cohesion and low coupling:

```text
📁 Scripts/
├── 📁 Data/           # ScriptableObjects (WeaponData, SkillData, EnemyData)
├── 📁 Player/
│   ├── 📁 StateMachine/ # Player State Pattern implementations
│   │   ├── PlayerStateMachine.cs (Context)
│   │   ├── PlayerMoveState.cs
│   │   ├── PlayerBlockState.cs
│   │   └── 📁 Skill/    # Weapon-specific modular skills
├── 📁 Enemy/          # Modular AI and Enemy State Machines
├── 📁 Core/           # Game Managers, Input Handling, Health/Stamina systems
└── 📁 UI/             # Dynamic Crosshairs, Healthbars, Damage Popups
```

## 🚀 Current Development Status

> **Note:** This project is actively being developed. Many visual assets (models, animations) are placeholders meant to test the robustness of the underlying code architecture.

**Recent Milestones Achieved:**
- [x] Full Player State Machine implementation.
- [x] Data-driven Weapon system (Melee & Ranged).
- [x] AAA-style Shield Blocking, Strafing, and Camera IK.
- [x] Initial Elemental Skill states (Ice, Lightning, Wind).

**Current Work-In-Progress:**
- [ ] Refactoring Enemy AI (Behavior Trees / State Machines) to sync perfectly with player attacks.
- [ ] Root Motion extraction for heavier attacks.
- [ ] Expanding the Inventory and Equipment UI systems.

## 🤝 Contact & Portfolio
I am currently looking for opportunities as a **Unity Developer / Gameplay Programmer**. If you find this architecture interesting, I would love to discuss it further!

**[Your Name]**
- 📧 Email: [Your Email]
- 💼 LinkedIn: [Your LinkedIn URL]
- 🌐 Portfolio: [Your Portfolio URL]

---
*Developed with passion.*
