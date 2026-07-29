# ISRO-HACKATHON

## Team: GenCode

## Team Members

| Name            | Hack2Skill Profile                                                                 | Role               |
|-----------------|-----------------------------------------------------------------------------------|--------------------|
| Sachin Patel    | [hack2skill.com/dashboard/user_public_profile/?userId=6a2f027622be8b44e661ab79](https://hack2skill.com/dashboard/user_public_profile/?userId=6a2f027622be8b44e661ab79&utm_source=hack2skill&utm_medium=homepage) | Developer          |
| Navneet Raj     | —                                                                                 | Developer          |
| Aniket Nandi    | —                                                                                 | Writer             |
| Shivam Jha      | —                                                                                 | Developer          |

> The ISRO-HACKATHON project is submitted to **Bharatiya Antariksh Hackathon (BAH) 2026** — the third edition of ISRO's national-level space-tech innovation challenge organized in collaboration with Hack2Skill.

## Description

ISRO-HACKATHON is a .NET Windows Forms application for infrared (IR) image processing and analysis, developed as part of the **Bharatiya Antariksh Hackathon (BAH) 2026**. The application leverages OpenCvSharp for image manipulation and Guna UI2 for a modern interface, providing real-time enhancements, segmentation overlays, road network visualization, and simulated object detection on IR imagery. The project addresses BAH 2026 challenge statements including **Infrared Image Colorization and Enhancement** and **Satellite Image Retrieval using Multi-Sensor Data**.

## Project Structure

```
ISRO-HACKATHON/
├── ISRO-HACKATHON/                  # Main application project
│   ├── Form1.cs                     # Main form and image processing logic
│   ├── Form1.Designer.cs            # UI designer file
│   ├── Helper.cs                    # Helper utilities
│   ├── Program.cs                   # Application entry point
│   ├── ISRO-HACKATHON.csproj        # Project file
│   ├── bin/                         # Build output
│   ├── obj/                         # Intermediate build objects
│   ├── Properties/                  # Resources and publish profiles
│   └── Resources/                   # Embedded image resources
├── img/                             # Output, logo, and icon images
│   ├── logo.png                     # Application logo
│   ├── icon.png                     # Application icon
│   ├── out.png                      # Output image 1
│   └── out1.png                     # Output image 2
├── ISRO-HACKATHON.slnx              # Solution file
└── README.md                        # This file
```

## Hackathon: Bharatiya Antariksh Hackathon (BAH) 2026

This project is submitted as part of the **Bharatiya Antariksh Hackathon (BAH) 2026**, the third edition of ISRO's national-level space-tech innovation challenge organized in collaboration with **Hack2Skill**.

### About BAH 2026

The Bharatiya Antariksh Hackathon invites undergraduate, postgraduate, and PhD students from recognised Indian institutions to solve real-world challenges in space technology, satellite imagery, climate science, AI/ML, and astronomy. Teams of 3–4 members develop innovative solutions and may receive mentorship from ISRO scientists and internship opportunities.

### Key Dates

| Event                          | Date                |
|--------------------------------|---------------------|
| Registration & Idea Submission | 10 June – 1 July 2026 |
| Problem Statement Explainer    | 15–16 June 2026     |
| Finalist Announcement          | 20 July 2026        |
| Induction Session              | 21 July 2026        |
| Grand Finale                   | 6–7 August 2026     |

### Challenge Addressed

This project addresses the following BAH 2026 problem statements:

- **Infrared Image Colorization and Enhancement** — AI/ML-based enhancement of IR satellite imagery
- **Satellite Image Retrieval using Multi-Sensor Data** — Multi-spectral fusion and retrieval systems

### Team Link

- **Sachin Patel's Hack2Skill Profile**: https://hack2skill.com/dashboard/user_public_profile/?userId=6a2f027622be8b44e661ab79

### Rewards & Benefits

- Mentorship from ISRO scientists and domain experts
- National-level recognition
- Potential internship opportunities with ISRO
- Travel reimbursement (II AC train fare) for finalists attending the Grand Finale

## Technologies

- **C#** / **.NET 10.0**
- **Windows Forms**
- **Guna UI2** v2.0.4.8
- **OpenCvSharp** v4.13.0

## Setup & Build

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
- Windows OS (Windows Forms target)

### Clone and Build

```bash
git clone <repository-url>
cd ISRO-HACKATHON
dotnet restore
dotnet build ISRO-HACKATHON/ISRO-HACKATHON.csproj
```

### Run

```bash
dotnet run --project ISRO-HACKATHON/ISRO-HACKATHON.csproj
```

### Publish (ClickOnce)

```powershell
cd ISRO-HACKATHON
dotnet publish -c Release -p:PublishProfile=ClickOnceProfile
```

The published output is located at `ISRO-HACKATHON/bin/Release/net10.0-windows/app.publish/`.

## Output Images

The `img/` folder contains the following output screenshots from the BAH 2026 project:

| File         | Description                         |
|--------------|-------------------------------------|
| `logo.png`   | Application logo                    |
| `icon.png`   | Application icon                    |
| `out.png`    | Sample processed output 1           |
| `out1.png`   | Sample processed output 2           |

### Output Previews

#### Output 1
![Output 1](../img/out.png)

#### Output 2
![Output 2](../img/out1.png)

## Icon

The application icon is stored in the `img/` folder as `icon.png`.

## License

© GenCode Team