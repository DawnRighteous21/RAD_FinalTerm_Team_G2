# Online Food Delivery Hub Platform (OFDH) — SaigonBite

**Course:** Requirements Analysis and Design (RAD) — Semester 01 (2026–2027)  
**Lecturer:** Mr. Pham Thai Ky Trung  
**Team:** Team G2 (Group DHT)  
**Case Study:** Online Food Delivery Hub Platform (OFDH) for SaigonBite

---

## 1. Project Overview

The **Online Food Delivery Hub Platform (OFDH)** is a centralized desktop software subsystem built with **C# WPF and SQLite** for **SaigonBite's** multi-brand cloud kitchens. The platform eliminates manual paper-ticket bottlenecks by automating capacity-aware order routing (busy-hub threshold ≥ 15 active orders and quiet-hub incentive ≤ 3 active orders), enabling barrier-free onboarding for foreign tourists via passport and email authentication (nullable local phone number), enforcing real-time order state tracking across kitchens and shippers, and providing full catalog CRUD and KPI reporting dashboards.

---

## 2. Team Members & Use Case Ownership

| Member       | Full Name      | Student ID | Role                                                       | Owned Use Cases (Case Study / Report)                                                                         |
| :----------- | :------------- | :--------- | :--------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------ |
| **Member 1** | Trần Quang Duy | `524H0012` | Lead System Analyst & Developer (Scrum Master Sprint 1)    | **UC3** (Report UC2): Place an order<br>**UC14** (Report UC5): Manage brands and menu items                   |
| **Member 2** | Lý Tín Hòa     | `524H0015` | Project Manager & QA Tester (Scrum Master Sprint 2)        | **UC8** (Report UC3): Prepare an order and mark it ready<br>**UC16** (Report UC6): Produce management reports |
| **Member 3** | Hà Dư Tuấn     | `524H0134` | UI/UX Designer & Database Engineer (Scrum Master Sprint 3) | **UC1** (Report UC1): Register customer and manage addresses<br>**UC10** (Report UC4): Deliver an order       |

---

## 3. Repository Structure

```text
SaigonBite-OFDH-TeamG2/
├── README.md
├── 00_Project_Management/
│   ├── Charts/
│   │   ├── GanttChart.png
│   │   └── OrganizationChart.png
│   ├── Part1_Proposal/
│   │   └── TeamG2_Proposal_for_SaigonBite.pdf
│   └── Workbook_And_Logs/
│       └── TeamG2_RAD_Team_Workbook.xlsx
├── 01_Shared_Models_Section_I_II/
│   ├── Codes/
│   │   ├── BusinessProcessFlowChart.puml
│   │   ├── DomainModelClassDiagram.puml
│   │   ├── SwimlaneActivityDiagram.puml
│   │   └── UseCaseDiagram.puml
│   └── Diagrams/
│       ├── BusinessProcessFlowchart.png
│       ├── DomainModelClassDiagram.png
│       ├── SwimlaneActivityDiagram.png
│       └── UseCaseDiagram.png
├── 02_Reports_And_Presentations/
│   └── Report/
│       ├── TeamG2_BRS_SRS_for_SaigonBite.pdf
│       └── TeamG2_Presentation_for_SaigonBite.pptx
├── 03_Shared_Models_Section_IV/
│   ├── Codes/
│   │   ├── SubsystemDesignClass.puml
│   │   ├── PackageDiagram.puml
│   │   └── DatabaseERD.puml
│   ├── Diagrams/
│   │   ├── SubsystemDesignClass.png
│   │   ├── PackageDiagram.png
│   │   └── DatabaseERD.png
│   └── SQL/
│       ├── 01_create_tables.sql
│       └── 02_seed_data.sql
├── src/
│   └── SaigonBite.OFDH/
│       ├── Views/                # WPF XAML screens (6 integrated use case tabs)
│       ├── Controllers/          # Application controllers matching Section III
│       ├── Domain/               # 13 persistent business entity classes
│       ├── DataAccess/           # SQLite DAOs, DatabaseConnection, and mock adapters
│       └── Tests/                # Post-condition verification suite (T-01 to T-24)
├── UC1_RegisterCustomer_524H0134/
│   ├── 01_Analysis/              # Codes/ & Diagrams/ (Activity, SSD, StateMachine)
│   ├── 02_Design/                # Codes/ & Diagrams/ (DesignClass, Communication, Sequence, FinalDesign)
│   ├── 03_Implementation_UI/     # Codes/ & Diagrams/ (ModifiedSequenceDiagram), UI/ & UI_Prototype_Link.txt
│   └── 04_Testing/               # Unit/integration test logs and evidence for UC1
├── UC3_PlaceOrder_524H0012/
│   ├── 01_Analysis/
│   ├── 02_Design/
│   ├── 03_Implementation_UI/
│   └── 04_Testing/
├── UC8_PrepareOrder_524H0015/
│   ├── 01_Analysis/
│   ├── 02_Design/
│   ├── 03_Implementation_UI/
│   └── 04_Testing/
├── UC10_DeliverOrder_524H0134/
│   ├── 01_Analysis/
│   ├── 02_Design/
│   ├── 03_Implementation_UI/
│   └── 04_Testing/
├── UC14_ManageBrandsMenus_524H0012/
│   ├── 01_Analysis/
│   ├── 02_Design/
│   ├── 03_Implementation_UI/
│   └── 04_Testing/
└── UC16_ProduceReports_524H0015/
    ├── 01_Analysis/
    ├── 02_Design/
    ├── 03_Implementation_UI/
    └── 04_Testing/
```

---

## 4. Key Deliverables & Links

- **BRS & SRS Report (PDF):** Available in `02_Reports_And_Presentations/Report/TeamG2_BRS_SRS_for_SaigonBite.pdf`
- **Presentation Slides (PPTX):** Available in `02_Reports_And_Presentations/Report/TeamG2_Presentation_for_SaigonBite.pptx`
- **Agile Sprint Workbook (Excel):** Available in `00_Project_Management/Workbook_And_Logs/TeamG2_RAD_Team_Workbook.xlsx`
- **Part 1 Project Proposal (PDF):** Available in `00_Project_Management/Part1_Proposal/TeamG2_Proposal_for_SaigonBite.pdf`
- **SQLite Schema & Seed Scripts:** Available in `03_Shared_Models_Section_IV/SQL/`
- **Interactive UI Prototype (Canva):** `[Paste Shared Canva Public View Link Here]`
- **Modelling Tool:** PlantUML (`.puml` source files included alongside exported `.png` diagrams)

---

## 5. Architecture & How to Run (Part 3 Implementation)

- **Tech Stack:** C# (.NET 8/9 WPF), SQLite (`Microsoft.Data.Sqlite`), PlantUML, Canva (UI Prototype).
- **Layered Architecture:** `Views (WPF)` → `Controllers` → `Domain` → `DataAccess (SQLite DAO)` → `saigonbite.db`.
- **Quick Start:**
  ```bash
  cd src/SaigonBite.OFDH
  dotnet restore
  dotnet run
  ```
  _On first launch, `DatabaseConnection` automatically initializes `saigonbite.db` using `03_Shared_Models_Section_IV/SQL/01_create_tables.sql` and `02_seed_data.sql`._
