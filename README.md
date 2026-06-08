# Friendly : PPO Reinforcement Learning based AI Horror Game
**플레이어의 반응에 따라 개인화된 공포 경험을 제공하는 AI 심리 공포게임**

[![Unity](https://img.shields.io/badge/Unity6-000000?style=flat-square&logo=unity&logoColor=white)](#)
[![C#](https://img.shields.io/badge/C%23-239120?style=flat-square&logo=c-sharp&logoColor=white)](#)
[![ML-Agents](https://img.shields.io/badge/Unity_ML--Agents-000000?style=flat-square&logo=unity&logoColor=white)](#)

<img width="800" height="510" alt="ReadMeGIF" src="https://github.com/user-attachments/assets/954a3397-5170-43a5-86bd-4716f98a9858" />

<br/>

## 📖 프로젝트 개요

**Friendly**는 스토리 기반 탐험형 심리 공포 게임에 AI 적응형 연출을 결합한 프로젝트입니다. 
플레이어는 야외, 납골당, 병원 등 다양한 공간을 탐험하며 사건의 진실을 추적합니다. 이 과정에서 게임은 플레이어의 **마우스 움직임과 마이크 반응을 실시간으로 분석**하여, 공포 이벤트의 종류와 타이밍을 동적으로 조절하는 **개인화된 공포 경험**을 제공합니다.

### 🚨 Pain Point & Solution
* **기존 공포 게임의 한계**: 고정된 스크립트 기반 연출로 인해 반복 플레이 시 패턴이 예측되어 지루해집니다. 또한, 플레이어마다 공포를 느끼는 요소가 다름에도 동일한 방식의 공포만을 제공합니다.
* **Friendly의 해결책**: 강화학습 모델(ML-Agents)을 활용하여 플레이어의 행동 패턴과 반응성을 학습합니다. 매 플레이마다 유저 성향에 맞춘 새로운 방식의 긴장감과 맞춤형 공포 시나리오를 제공하여 반복 플레이의 가치를 높입니다.

<br/>

## 🎬 스토리 
친밀한 대학 동기의 죽은 부친의 기일, 함께 찾은 납골당에서 친구가 갑자기 사라진다. 옆 폐병원에 들어온 순간, 문은 닫혀버린다. 병원을 탐색하며 발견한 기록들은 이곳의 의사였던 나의 아버지와 친구 아버지의 죽음이 얽혀 있다고 말한다. 믿을 수 없는 단서들을 두고, 복수하기만을 기다려 온 친구를 피해 진실을 밝혀내야 한다.

#### 👥 등장인물

| Jackie Stevens(친구) | Yujin Mogan(주인공 플레이어) |
| :---: | :---: |
| <img src="https://github.com/user-attachments/assets/7ddf6860-b3ba-4392-9277-eca480d2847b" width="300" height="346" alt="NPC 이미지"> | <img src="https://github.com/user-attachments/assets/6ba7b4be-553e-4bd1-86c1-212811a73b66" width="300" height="346" alt="PC 이미지"> |

<br/>

## ✨ 주요 기능

### 🧠 AI 기반 동적 공포 연출 
* **Fear Signal 분석**: 플레이어의 마우스의 움직임과 마이크 데시벨을 실시간으로 수집합니다. 개인별 Baseline을 보정하여 환경 소음을 제거하고, 플레이어의 공포 반응을 Fear Signal로 수치화합니다.
* **개인화된 맞춤형 공포**: 분석된 반응 수치와 플레이어의 위치 정보를 바탕으로 AI가 다음 공포 연출(마네킹, 조명, 점프스케어, 소리, 문, 오브젝트 변형 등)의 종류와 타이밍을 결정합니다.

### 🔦 탐험 및 상호작용 시스템
* **다양한 환경 탐험**: 스토리가 진행됨에 따라 변화하는 씬(야외, 납골당, 병원)을 탐험하며 단서를 수집합니다.
* **퍼즐 및 인벤토리**: 획득한 아이템을 인벤토리에서 관리하고, 키패드 조작 등 다양한 인터랙티브 퍼즐을 해결해야 합니다.
  
### 📜 데이터 기반 스토리 진행 
* **데이터 관리**: JSON 기반으로 방대한 대화 및 아이템 정보를 효율적으로 관리합니다.
* **유동적 분기**: 플래그와 변수를 통해 플레이어의 선택 및 상호작용에 따라 이벤트가 다양하게 분기됩니다.

<br/>

## 🎯 타겟 유저
* 스토리 중심의 탐험형 게임을 선호하는 유저
* 점프스케어를 넘어 심리적 긴장감과 분위기형 공포를 즐기는 유저
* 다회차 플레이에서도 예측할 수 없는 새로운 공포 경험을 원하는 유저

<br/>

## 🛠 기술 스택
* **Game Engine**: Unity 6
* **Programming**: C#
* **AI & Machine Learning**: Unity ML-Agents Toolkit
* **Data Management**: JSON

<br/>

## 👨‍💻 팀원 소개 및 역할 (Team 14-BestFriend)

| 이름 | 역할 및 담당 업무 |
| :---: | :--- |
| **양동선** | 팀 리더 / 프로젝트 기획 / Unity 클라이언트 개발 / 자료 제출 |
| **박주영** | 프로젝트 기획 / Unity 클라이언트 개발 / 스크립트 작성 및 2D 아트 에셋 제작 |
| **윤소진** | 프로젝트 기획 / Unity 클라이언트 개발 / AI 모델(ML-Agents) 설계 및 구현 |

<br/>

## 📌 기대 효과
1. 플레이어 반응 기반의 동적 공포 조절로 예측 불가능한 긴장감 유지
2. AI 공포 연출 구조로 공포 게임의 새로운 패러다임 제시
3. 다회차 플레이 시 매번 달라지는 이벤트 타이밍과 조합으로 재플레이 가치 상승

<br/>
## 🎮 How to Build & Play

### Play
웹 플레이 링크: [itch.io 링크]

### Build from Source
1. Unity 6에서 프로젝트 열기
2. File → Build Profiles 
3. Platform: Windows 선택
4. Scene List에서 BootstrapScene, TitleScene, OutdoorScene, OssuaryIndoorScene, 10_F1_Main, 11_F1_CCTVInterior, 2F_Hall, 12_F1_Main 씬 추가
5. Build 클릭
6. 빌드본이 있는 폴더의 Friendly_0206.exe 실행 후 게임 플레이  
