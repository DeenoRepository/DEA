# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v11.0.

## Table of Contents

- [Executive Summary](#executive-Summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
- [Aggregate NuGet packages details](#aggregate-nuget-packages-details)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Projects Relationship Graph](#projects-relationship-graph)
- [Project Details](#project-details)

  - [EquipmentFailureAnalysis\EquipmentFailureAnalysis.csproj](#equipmentfailureanalysisequipmentfailureanalysiscsproj)


## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 1 | All require upgrade |
| Total NuGet Packages | 6 | All compatible |
| Total Code Files | 20 |  |
| Total Code Files with Incidents | 3 |  |
| Total Lines of Code | 1586 |  |
| Total Number of Issues | 7 |  |
| Estimated LOC to modify | 6+ | at least 0,4% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Package Issues | API Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| [EquipmentFailureAnalysis\EquipmentFailureAnalysis.csproj](#equipmentfailureanalysisequipmentfailureanalysiscsproj) | net10.0 | 🟢 Low | 0 | 6 | 6+ | WinForms, Sdk Style = True |

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 6 | 100,0% |
| ⚠️ Incompatible | 0 | 0,0% |
| 🔄 Upgrade Recommended | 0 | 0,0% |
| ***Total NuGet Packages*** | ***6*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 6 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 2745 |  |
| ***Total APIs Analyzed*** | ***2751*** |  |

## Aggregate NuGet packages details

| Package | Current Version | Suggested Version | Projects | Description |
| :--- | :---: | :---: | :--- | :--- |
| Avalonia | 11.3.11 |  | [EquipmentFailureAnalysis.csproj](#equipmentfailureanalysisequipmentfailureanalysiscsproj) | ✅Compatible |
| Avalonia.Desktop | 11.3.11 |  | [EquipmentFailureAnalysis.csproj](#equipmentfailureanalysisequipmentfailureanalysiscsproj) | ✅Compatible |
| Avalonia.Diagnostics | 11.3.11 |  | [EquipmentFailureAnalysis.csproj](#equipmentfailureanalysisequipmentfailureanalysiscsproj) | ✅Compatible |
| Avalonia.Fonts.Inter | 11.3.11 |  | [EquipmentFailureAnalysis.csproj](#equipmentfailureanalysisequipmentfailureanalysiscsproj) | ✅Compatible |
| Avalonia.Themes.Fluent | 11.3.11 |  | [EquipmentFailureAnalysis.csproj](#equipmentfailureanalysisequipmentfailureanalysiscsproj) | ✅Compatible |
| ReactiveUI.Avalonia | 11.3.8 |  | [EquipmentFailureAnalysis.csproj](#equipmentfailureanalysisequipmentfailureanalysiscsproj) | ✅Compatible |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |
| M:System.TimeSpan.FromMinutes(System.Int64) | 4 | 66,7% | Source Incompatible |
| M:System.TimeSpan.FromHours(System.Double) | 2 | 33,3% | Source Incompatible |

## Projects Relationship Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart LR
    P1["<b>📦&nbsp;EquipmentFailureAnalysis.csproj</b><br/><small>net10.0</small>"]
    click P1 "#equipmentfailureanalysisequipmentfailureanalysiscsproj"

```

## Project Details

<a id="equipmentfailureanalysisequipmentfailureanalysiscsproj"></a>
### EquipmentFailureAnalysis\EquipmentFailureAnalysis.csproj

#### Project Info

- **Current Target Framework:** net10.0
- **Proposed Target Framework:** net11.0-windows
- **SDK-style**: True
- **Project Kind:** WinForms
- **Dependencies**: 0
- **Dependants**: 0
- **Number of Files**: 20
- **Number of Files with Incidents**: 3
- **Lines of Code**: 1586
- **Estimated LOC to modify**: 6+ (at least 0,4% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["EquipmentFailureAnalysis.csproj"]
        MAIN["<b>📦&nbsp;EquipmentFailureAnalysis.csproj</b><br/><small>net10.0</small>"]
        click MAIN "#equipmentfailureanalysisequipmentfailureanalysiscsproj"
    end

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 6 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 2745 |  |
| ***Total APIs Analyzed*** | ***2751*** |  |

