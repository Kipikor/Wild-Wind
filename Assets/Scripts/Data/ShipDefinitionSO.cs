using UnityEngine;

[CreateAssetMenu(fileName = "NewShipDefinition", menuName = "Wild Wind/Ships/Ship Definition")]
public class ShipDefinitionSO : ScriptableObject
{
    [Header("Meta")]
    public string shipId = "starter_ship";
    public string displayName = "Starter Ship";
    [TextArea] public string description = "";
    public int tier = 1;
    public int purchasePrice = 0;
    public int unlockCost = 0;

    [Header("Flight Model")]
    public ShipFlightTuning flight = new ShipFlightTuning();

    [Header("Modules")]
    public ShipEngineTuning thrustEngine = new ShipEngineTuning();
    public ShipEngineTuning liftEngine = new ShipEngineTuning();
    public ShipBalloonTuning balloon = new ShipBalloonTuning();
    public ShipClaudiumLoopTuning claudiumLoop = new ShipClaudiumLoopTuning();

    public void CaptureFrom(ShipPhysics ship)
    {
        if (ship == null) return;

        displayName = ship.gameObject.name;
        shipId = MakeId(displayName);

        flight.CaptureFrom(ship);
        thrustEngine.CaptureFrom(ship.thrustEngine);
        liftEngine.CaptureFrom(ship.liftEngine);
        balloon.CaptureFrom(ship.balloonModule);
        claudiumLoop.CaptureFrom(ship.claudiumLoop);
    }

    public void ApplyTo(ShipPhysics ship)
    {
        if (ship == null) return;

        flight.ApplyTo(ship);
        thrustEngine.ApplyTo(ship.thrustEngine);
        liftEngine.ApplyTo(ship.liftEngine);
        balloon.ApplyTo(ship.balloonModule);
        claudiumLoop.ApplyTo(ship.claudiumLoop);
    }

    private static string MakeId(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "ship";
        return source.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}

[System.Serializable]
public class ShipFlightTuning
{
    [Header("Mass")]
    public float baseMass = 1000f;
    public float targetTrimMass = 1000f;

    [Header("Propeller")]
    public float propellerDiameter = 4.5f;
    public float propellerEfficiency = 0.8f;
    public float propellerMaxPitchMeters = 3.0f;
    public float initialMainEngineRPM = 0.8f;
    public bool hasCSU = false;

    [Header("Lift")]
    public float liftEfficiency = 20f;
    public float maxStructuralVerticalSpeed = 5.0f;
    public float maxAutoVerticalSpeed = 1.0f;

    [Header("Aerodynamics")]
    public float airDensity = 1.225f;
    public float dragCoefficient = 1.1f;
    public float frontalArea = 6.3f;
    public float sideResistance = 2.0f;
    public float verticalAreaFactor = 4.0f;

    [Header("Rudder")]
    public float rudderArea = 4.0f;
    public float rudderDistance = 10.0f;
    public float rudderMaxLiftCoeff = 1.5f;
    public float maxRudderAngleDeg = 25f;
    public float rudderTurnSpeedDeg = 15f;
    public float maxAutoTurnRateDeg = 5.0f;
    public float maxStructuralTurnRateDeg = 15.0f;

    [Header("Autopilot Defaults")]
    public bool autoStabilizeAtStart = true;
    public bool altitudeHold = false;
    public float altStiffness = 0.2f;
    public float altDamping = 1.2f;
    public float altDriftTolerance = 0.15f;
    public bool cruiseControl = false;
    public float maxCruiseSpeedMS = 20f;
    public float maxManualSpeedMS = 30f;
    public bool headingHold = false;
    public float headingStiffness = 0.5f;
    public float headingDamping = 0.5f;
    public float waypointRadius = 10f;
    public float minNavSpeed = 5f;
    public float speedStiffness = 0.8f;
    public float speedDamping = 0.3f;

    public void CaptureFrom(ShipPhysics ship)
    {
        if (ship == null) return;

        baseMass = ship.baseMass;
        targetTrimMass = ship.targetTrimMass;
        propellerDiameter = ship.propellerDiameter;
        propellerEfficiency = ship.propellerEfficiency;
        propellerMaxPitchMeters = ship.propellerMaxPitchMeters;
        initialMainEngineRPM = ship.targetMainEngineRPM;
        hasCSU = ship.hasCSU;
        liftEfficiency = ship.liftEfficiency;
        maxStructuralVerticalSpeed = ship.maxStructuralVerticalSpeed;
        maxAutoVerticalSpeed = ship.maxAutoVerticalSpeed;
        airDensity = ship.airDensity;
        dragCoefficient = ship.dragCoefficient;
        frontalArea = ship.frontalArea;
        sideResistance = ship.sideResistance;
        verticalAreaFactor = ship.verticalAreaFactor;
        rudderArea = ship.rudderArea;
        rudderDistance = ship.rudderDistance;
        rudderMaxLiftCoeff = ship.rudderMaxLiftCoeff;
        maxRudderAngleDeg = ship.maxRudderAngleDeg;
        rudderTurnSpeedDeg = ship.rudderTurnSpeedDeg;
        maxAutoTurnRateDeg = ship.maxAutoTurnRateDeg;
        maxStructuralTurnRateDeg = ship.maxStructuralTurnRateDeg;
        autoStabilizeAtStart = ship.autoStabilizeAtStart;
        altitudeHold = ship.altitudeHold;
        altStiffness = ship.altStiffness;
        altDamping = ship.altDamping;
        altDriftTolerance = ship.altDriftTolerance;
        cruiseControl = ship.cruiseControl;
        maxCruiseSpeedMS = ship.maxCruiseSpeedMS;
        maxManualSpeedMS = ship.maxManualSpeedMS;
        headingHold = ship.headingHold;
        headingStiffness = ship.headingStiffness;
        headingDamping = ship.headingDamping;
        waypointRadius = ship.waypointRadius;
        minNavSpeed = ship.minNavSpeed;
        speedStiffness = ship.speedStiffness;
        speedDamping = ship.speedDamping;
    }

    public void ApplyTo(ShipPhysics ship)
    {
        if (ship == null) return;

        ship.baseMass = baseMass;
        ship.targetTrimMass = targetTrimMass;
        ship.propellerDiameter = propellerDiameter;
        ship.propellerEfficiency = propellerEfficiency;
        ship.propellerMaxPitchMeters = propellerMaxPitchMeters;
        ship.targetMainEngineRPM = initialMainEngineRPM;
        ship.hasCSU = hasCSU;
        ship.liftEfficiency = liftEfficiency;
        ship.maxStructuralVerticalSpeed = maxStructuralVerticalSpeed;
        ship.maxAutoVerticalSpeed = maxAutoVerticalSpeed;
        ship.airDensity = airDensity;
        ship.dragCoefficient = dragCoefficient;
        ship.frontalArea = frontalArea;
        ship.sideResistance = sideResistance;
        ship.verticalAreaFactor = verticalAreaFactor;
        ship.rudderArea = rudderArea;
        ship.rudderDistance = rudderDistance;
        ship.rudderMaxLiftCoeff = rudderMaxLiftCoeff;
        ship.maxRudderAngleDeg = maxRudderAngleDeg;
        ship.rudderTurnSpeedDeg = rudderTurnSpeedDeg;
        ship.maxAutoTurnRateDeg = maxAutoTurnRateDeg;
        ship.maxStructuralTurnRateDeg = maxStructuralTurnRateDeg;
        ship.autoStabilizeAtStart = autoStabilizeAtStart;
        ship.altitudeHold = altitudeHold;
        ship.altStiffness = altStiffness;
        ship.altDamping = altDamping;
        ship.altDriftTolerance = altDriftTolerance;
        ship.cruiseControl = cruiseControl;
        ship.maxCruiseSpeedMS = maxCruiseSpeedMS;
        ship.maxManualSpeedMS = maxManualSpeedMS;
        ship.headingHold = headingHold;
        ship.headingStiffness = headingStiffness;
        ship.headingDamping = headingDamping;
        ship.waypointRadius = waypointRadius;
        ship.minNavSpeed = minNavSpeed;
        ship.speedStiffness = speedStiffness;
        ship.speedDamping = speedDamping;
    }
}

[System.Serializable]
public class ShipEngineTuning
{
    public string engineName = "Standard Engine";
    public float maxPower = 60f;
    public float maxRPM = 2500f;
    public float responsiveness = 0.5f;
    [Range(0f, 1.2f)] public float startingRPM = 0.5f;
    [Range(0.01f, 0.9f)] public float efficiency = 0.15f;
    public bool isClaudium = false;

    public void CaptureFrom(ShipEngine engine)
    {
        if (engine == null) return;

        engineName = engine.engineName;
        maxPower = engine.maxPower;
        maxRPM = engine.maxRPM;
        responsiveness = engine.responsiveness;
        startingRPM = engine.startingRPM;
        efficiency = engine.efficiency;
        isClaudium = engine.isClaudium;
    }

    public void ApplyTo(ShipEngine engine)
    {
        if (engine == null) return;

        engine.engineName = engineName;
        engine.maxPower = maxPower;
        engine.maxRPM = maxRPM;
        engine.responsiveness = responsiveness;
        engine.startingRPM = startingRPM;
        engine.targetRPM = startingRPM;
        engine.efficiency = efficiency;
        engine.isClaudium = isClaudium;

        if (!Application.isPlaying)
        {
            engine.currentRPM = startingRPM;
        }
    }
}

[System.Serializable]
public class ShipBalloonTuning
{
    public string balloonName = "Standard Balloon";
    public float diameterM = 8.0f;
    public float lengthM = 12.0f;
    [Range(0f, 100f)] public float fillPercent = 0f;
    public float leakM3PerHour = 1.0f;
    public float valveFlowRate = 0.5f;

    public void CaptureFrom(ShipBalloon balloon)
    {
        if (balloon == null) return;

        balloonName = balloon.названиеБаллона;
        diameterM = balloon.диаметрМ;
        lengthM = balloon.длинаМ;
        fillPercent = balloon.процентЗаполнения;
        leakM3PerHour = balloon.утечкаМ3вЧас;
        valveFlowRate = balloon.valveFlowRate;
    }

    public void ApplyTo(ShipBalloon balloon)
    {
        if (balloon == null) return;

        balloon.названиеБаллона = balloonName;
        balloon.диаметрМ = diameterM;
        balloon.длинаМ = lengthM;
        balloon.процентЗаполнения = fillPercent;
        balloon.утечкаМ3вЧас = leakM3PerHour;
        balloon.valveFlowRate = valveFlowRate;
        balloon.CalculateGeometry();
    }
}

[System.Serializable]
public class ShipClaudiumLoopTuning
{
    public string loopName = "Claudium Loop";
    public float systemVolumeL = 50f;
    public float loopLengthM = 25f;
    [Range(0f, 100f)] public float concentration = 10f;
    [Range(0f, 100f)] public float targetConcentration = 10f;
    public float solutionStockL = 100f;
    public float solutionDensity = 1.2f;
    public float crystalStockKg = 5.0f;
    public float dissolutionSpeedKgPerMinute = 0.5f;
    public float initialTemperatureC = 20f;
    public float externalHeatWatts = 0f;
    public bool useEngineWasteHeat = true;
    public float heatLoss = 0.05f;
    public float maxPressureBar = 200f;
    [Range(0f, 1f)] public float efficiency = 0.85f;

    public void CaptureFrom(ShipClaudiumLoop loop)
    {
        if (loop == null) return;

        loopName = loop.названиеКонтура;
        systemVolumeL = loop.объемСистемыЛ;
        loopLengthM = loop.длинаКонтураМ;
        concentration = loop.концентрацияКлавдия;
        targetConcentration = loop.целеваяКонцентрация;
        solutionStockL = loop.запасРаствораЛ;
        solutionDensity = loop.плотностьРаствора;
        crystalStockKg = loop.запасКристалловКг;
        dissolutionSpeedKgPerMinute = loop.скоростьРастворения;
        initialTemperatureC = loop.текущаяТемпература;
        externalHeatWatts = loop.внешнийПодогревВт;
        useEngineWasteHeat = loop.забиратьТеплоОтДвигателя;
        heatLoss = loop.коэфТеплопотери;
        maxPressureBar = loop.максДавлениеБар;
        efficiency = loop.кпдКонтура;
    }

    public void ApplyTo(ShipClaudiumLoop loop)
    {
        if (loop == null) return;

        loop.названиеКонтура = loopName;
        loop.объемСистемыЛ = systemVolumeL;
        loop.длинаКонтураМ = loopLengthM;
        loop.концентрацияКлавдия = concentration;
        loop.целеваяКонцентрация = targetConcentration;
        loop.запасРаствораЛ = solutionStockL;
        loop.плотностьРаствора = solutionDensity;
        loop.запасКристалловКг = crystalStockKg;
        loop.скоростьРастворения = dissolutionSpeedKgPerMinute;
        loop.текущаяТемпература = initialTemperatureC;
        loop.внешнийПодогревВт = externalHeatWatts;
        loop.забиратьТеплоОтДвигателя = useEngineWasteHeat;
        loop.коэфТеплопотери = heatLoss;
        loop.максДавлениеБар = maxPressureBar;
        loop.кпдКонтура = efficiency;
    }
}
