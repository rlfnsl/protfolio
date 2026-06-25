# Metal Cardbot Dual Arena

Unity arcade hardware game with RF card, QR, serial device, and custom build flow.

## Role

Main developer. Estimated contribution: about 60%.

## Main Responsibilities

- RF card and QR data flow
- SerialPort card dispense/payment device integration
- Google Sheet based game data loading
- Battle score and rank calculation
- Computer, Arcade, and ArcadeTest build menus
- Real device play-flow handling

## Runtime Flow

```mermaid
flowchart TD
    Boot["Start / Mode"] --> Data["Google Sheet DataManager"]
    Data --> QR["QR / Skill Card Data"]
    QR --> Card["Card Setting"]
    Device["Arcade Device"] --> Serial["SerialPort / XML Config"]
    Serial --> Trade["CardTrading"]
    Trade --> Payment["Coin / Payment / Dispose"]
    Card --> Battle["Battle"]
    Battle --> Score["Score / Rank"]
    Score --> Ending["Ending / Ranking"]
    Build["CustomBuild"] --> PC["Computer"]
    Build --> Arcade["ARCADE"]
    Build --> Test["ARCADETest"]
```

## Code Evidence

- `CardTrading.cs`: SerialPort, XML config, card dispense/payment flow
- `CardTest.cs`: QR scan to game card data
- `DataManager.cs`: Google Sheet TSV loading for stage, monster, skill, QR, sound, level data
- `BattleCalculator.cs`: combo, damage, time, HP score/rank calculation
- `Editor/CustomBuild.cs`: separated build menus and defines

## Representative Code Samples

- `Samples/Hardware/ArcadeCardDeviceFlowSample.cs`

