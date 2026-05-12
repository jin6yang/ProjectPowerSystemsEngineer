# ProjectPSE - Player Guide

> **Foreword**
>
> This is a voxel-style business simulation and puzzle-solving game.



## Game Background

OBSIDIAN Industrial, a subsidiary of the massive interstellar corporation OBSIDIAN.INC, is currently recruiting new talent. You are an ordinary applicant vying for the position of "Power Systems Engineer." To join the company as a formal employee, you must operate the induction assessment system and successfully pass the evaluation.



## Basic Rules

1. Players must complete assessment objectives by managing `Power Output (MW)` and `Stability Values (STB)`.

2. Game levels take place on a sandbox divided by a grid system, where players can place buildings.

3. Power generation buildings cannot be connected directly; they must be bridged through other intermediary buildings.

4. Distributed Energy Storage: Multiple adjacent storage buildings are treated as a single large resource pool, whereas separate storage clusters within the same grid are treated as distinct resource pools.

5. If a building or device exceeds its load capacity, the automatic power-off protection will trigger.



## Controls

> Current version **does not support** custom keybindings.

### Keyboard and Mouse

| Action          | Keyboard | Mouse                                |
| :-------------- | :------- | :----------------------------------- |
| Move Camera     | W/S/A/D  | Left-click and drag / Edge scrolling |
| Zoom Camera     |          | Scroll Wheel                         |
| Rotate Camera   | Q/E      | Right-click and drag                 |
| Delete Building | DEL      |                                      |

### Controller

> **Recommended** to use `Steam Controller` or perform custom **mapping**.

| Action          | Controller                              |
| :-------------- | :-------------------------------------- |
| Move Camera     | Right Stick                             |
| Zoom Camera     | RT/LT                                   |
| Rotate Camera   | Press and hold Right Stick while moving |
| Delete Building |                                         |



## Building List and Values

> Any values not mentioned in the list default to **0**.

### A. Generation

| **Component Name**       | **Appearance** | **Output (MW)** | Load Capacity (MW) |
| :----------------------- | :------------- | :-------------- | :----------------- |
| **Micro Fusion Reactor** | Orange         | 150             | 500                |
| **Solar Panel Array**    | Blue           | 75              | 500                |

### B. Transmission

| **Component Name**        | **Appearance** | Stability Bonus (STB) | Load Capacity (MW) | **Features**              |
| :------------------------ | :------------- | :-------------------- | :----------------- | :------------------------ |
| **Standard Cable**        | Copper         |                       | 250                | Connection range: 4 grids |
| **Relay Tower**           | Grey           |                       | 1000               |                           |
| **Standard Stabilizer**   | Blue           | 1                     | 1000               |                           |
| **Long-range Stabilizer** | Purple         | 3                     | 1000               |                           |

### C. Storage & Regulation

| **Component Name**                       | **Appearance** | **STB Bonus** | **Output (MW)** | Load Cap (MW) | Max Charge (S) | Max Discharge (S) | **Features**      |
| :--------------------------------------- | :------------- | :------------ | :-------------- | :------------ | :------------- | :---------------- | :---------------- |
| **Battery Energy Storage System (BESS)** | Green          | 2             | 25              | 500           | 2              | 4                 | Effects stackable |

### D. Consumers / Objectives

> **Features**: Cannot be moved or removed by the player.

| **Component Name**      | **Appearance** | STB Requirement | Power Demand (MW) | Load Cap (MW) |
| :---------------------- | :------------- | :-------------- | :---------------- | :------------ |
| **City Grid Interface** | Grey           | 2/6             | 150/200           | 500           |
| **AI Core - Hub**       | Grey           | 6               | 200               | 500           |
| **AI Core - Storage**   | Grey           | 3               | 200               | 500           |
| **AI Core - Cooling**   | Grey           | 3               | 200               | 500           |



## Special Commands (Development Use)

1. Press the `~` key on the Level Selection page of the Main Menu to enter the Developer Testing Level.

2. Press the `~` key during a level to immediately complete the mission (Victory).

3. Press and hold the `~` key for `3s` to immediately fail the current level (Failure).



## Game Story

EN: [Pre-assessment Instructions (Game Story)](Pre-assessment_Instructions-(Game Story).md)

ZH: [考前须知（游戏剧情）](考前须知（游戏剧情）.md)

Please **read carefully** the pre-assessment instructions before beginning your employee evaluation!
