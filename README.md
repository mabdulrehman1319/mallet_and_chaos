# mallet_and_chaos

A 2D top-down hockey-style game built in C# WinForms, featuring 4v4 team play with AI-controlled and human-controlled players, role-based behavior, and physics-based ball hitting. Match, round, goal, and player stats persist to a SQL Server database via ADO.NET.

## Features
- 2 teams of 4 players (mix of AI and human-controlled)
- Role-based player behavior: Defender, Scorer, Captain, Helper
- Adjustable AI difficulty (Low, Medium, Hard)
- Practice mode that walks through each role step by step
- Physics-based ball movement and hitting
- Match, round, goal, and player stat tracking saved to SQL Server

## Tech Stack
- **Language:** C# (.NET Framework 4.8)
- **UI:** WinForms
- **Database:** SQL Server, via ADO.NET (`System.Data.SqlClient`)

## Project Structure
```
mallet_and_chaos/
├── POLO.slnx
├── POLO Game DB.sql
├── README.md
└── POLO/
    ├── POLO.csproj
    ├── App.config
    ├── Program.cs
    ├── DatabaseHelper.cs
    └── Properties/
        ├── AssemblyInfo.cs
        ├── Resources.resx
        ├── Resources.Designer.cs
        ├── Settings.settings
        └── Settings.Designer.cs
```
## Setup

1. **Clone the repository**
```bash
   git clone https://github.com/mabdulrehman1319/mallet_and_chaos.git
```

2. **Set up the database**
   - Open SQL Server Management Studio
   - Run `POLO Game DB.sql` to create the `POLOGAME_DB` database and tables

3. **Update the connection string**
   - Open `POLO/DatabaseHelper.cs`
   - Update the `Server=` value in `CONNECTION_STRING` to match your own SQL Server instance name

4. **Run the project**
   - Open `POLO.slnx` in Visual Studio
   - Build and run (F5)

## Controls
| Key | Action |
|-----|--------|
| Arrow Keys | Move active player |
| Space | Hit ball |
| Tab | Switch active player |
| P | Pause / Resume |
| Esc | Back to menu |
| R | Restart (game-over screen) |

## Requirements
- Windows OS
- Visual Studio (with .NET Framework 4.8 support)
- SQL Server

## Author
Muhammad Abdul Rehman
