using UnityEngine;

[CreateAssetMenu(fileName = "НовыйПаспортКорабля", menuName = "Wild Wind/Корабли/Паспорт корабля")]
public class ShipDefinitionSO : ScriptableObject
{
    [Header("Мета")]
    [InspectorName("Идентификатор корабля")]
    [Tooltip("Технический идентификатор корабля. Используется в сохранениях и древе техники.")]
    public string shipId = "starter_ship";
    [InspectorName("Название")]
    public string displayName = "Стартовый корабль";
    [InspectorName("Описание")]
    [TextArea] public string description = "";
    [InspectorName("Уровень")]
    public int tier = 1;
    [InspectorName("Цена покупки")]
    public int purchasePrice = 0;
    [InspectorName("Стоимость открытия")]
    [Tooltip("Резервное поле для будущей логики открытия корабля отдельно от покупки.")]
    public int unlockCost = 0;

    [Header("Летная модель")]
    [InspectorName("Настройки полета")]
    public ShipFlightTuning flight = new ShipFlightTuning();

    [Header("Модули")]
    [InspectorName("Маршевый двигатель")]
    public ShipEngineTuning thrustEngine = new ShipEngineTuning();
    [InspectorName("Подъемный двигатель")]
    public ShipEngineTuning liftEngine = new ShipEngineTuning();
    [InspectorName("Баллон")]
    public ShipBalloonTuning balloon = new ShipBalloonTuning();
    [InspectorName("Клавдиевый контур")]
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
    [Header("Масса")]
    [InspectorName("Базовая масса")]
    public float baseMass = 1000f;
    [InspectorName("Масса триммирования")]
    [Tooltip("Масса, под которую система подъема старается сбалансировать корабль.")]
    public float targetTrimMass = 1000f;

    [Header("Винт")]
    [InspectorName("Диаметр винта")]
    public float propellerDiameter = 4.5f;
    [InspectorName("КПД винта")]
    public float propellerEfficiency = 0.8f;
    [InspectorName("Максимальный шаг винта, м")]
    public float propellerMaxPitchMeters = 3.0f;
    [InspectorName("Стартовые обороты маршевого двигателя")]
    public float initialMainEngineRPM = 0.8f;
    [InspectorName("Есть автомат шага винта")]
    [Tooltip("Автомат шага винта управляет шагом сам и удерживает целевые обороты двигателя.")]
    public bool hasCSU = false;

    [Header("Подъем")]
    [InspectorName("Эффективность подъема")]
    public float liftEfficiency = 20f;
    [InspectorName("Конструкционный лимит вертикальной скорости")]
    public float maxStructuralVerticalSpeed = 5.0f;
    [InspectorName("Лимит вертикальной скорости автопилота")]
    public float maxAutoVerticalSpeed = 1.0f;

    [Header("Аэродинамика")]
    [InspectorName("Плотность воздуха")]
    public float airDensity = 1.225f;
    [InspectorName("Коэффициент сопротивления")]
    public float dragCoefficient = 1.1f;
    [InspectorName("Лобовая площадь")]
    public float frontalArea = 6.3f;
    [InspectorName("Боковое сопротивление")]
    public float sideResistance = 2.0f;
    [InspectorName("Множитель вертикальной площади")]
    public float verticalAreaFactor = 4.0f;

    [Header("Рули")]
    [InspectorName("Площадь рулей")]
    public float rudderArea = 4.0f;
    [InspectorName("Плечо рулей")]
    public float rudderDistance = 10.0f;
    [InspectorName("Макс. коэффициент подъемной силы руля")]
    public float rudderMaxLiftCoeff = 1.5f;
    [InspectorName("Макс. угол руля")]
    public float maxRudderAngleDeg = 25f;
    [InspectorName("Скорость перекладки руля")]
    public float rudderTurnSpeedDeg = 15f;
    [InspectorName("Макс. скорость поворота автопилота")]
    public float maxAutoTurnRateDeg = 5.0f;
    [InspectorName("Конструкционный лимит поворота")]
    public float maxStructuralTurnRateDeg = 15.0f;

    [Header("Автопилот по умолчанию")]
    [InspectorName("Автостабилизация при старте")]
    public bool autoStabilizeAtStart = true;
    [InspectorName("Удержание высоты")]
    public bool altitudeHold = false;
    [InspectorName("Жесткость высоты")]
    public float altStiffness = 0.2f;
    [InspectorName("Демпфирование высоты")]
    public float altDamping = 1.2f;
    [InspectorName("Допуск дрейфа высоты")]
    public float altDriftTolerance = 0.15f;
    [InspectorName("Круиз-контроль")]
    public bool cruiseControl = false;
    [InspectorName("Макс. скорость круиза")]
    public float maxCruiseSpeedMS = 20f;
    [InspectorName("Макс. ручная скорость")]
    public float maxManualSpeedMS = 30f;
    [InspectorName("Удержание курса")]
    public bool headingHold = false;
    [InspectorName("Жесткость курса")]
    public float headingStiffness = 0.5f;
    [InspectorName("Демпфирование курса")]
    public float headingDamping = 0.5f;
    [InspectorName("Радиус точки маршрута")]
    public float waypointRadius = 10f;
    [InspectorName("Минимальная скорость навигации")]
    public float minNavSpeed = 5f;
    [InspectorName("Жесткость скорости")]
    public float speedStiffness = 0.8f;
    [InspectorName("Демпфирование скорости")]
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
    [InspectorName("Название двигателя")]
    public string engineName = "Стандартный двигатель";
    [InspectorName("Максимальная мощность")]
    public float maxPower = 60f;
    [InspectorName("Максимальные обороты")]
    public float maxRPM = 2500f;
    [InspectorName("Отзывчивость")]
    public float responsiveness = 0.5f;
    [InspectorName("Стартовые обороты")]
    [Range(0f, 1.2f)] public float startingRPM = 0.5f;
    [InspectorName("КПД")]
    [Range(0.01f, 0.9f)] public float efficiency = 0.15f;
    [InspectorName("Клавдиевый двигатель")]
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
    [InspectorName("Название баллона")]
    public string balloonName = "Стандартный баллон";
    [InspectorName("Диаметр, м")]
    public float diameterM = 8.0f;
    [InspectorName("Длина, м")]
    public float lengthM = 12.0f;
    [InspectorName("Заполнение, %")]
    [Range(0f, 100f)] public float fillPercent = 0f;
    [InspectorName("Утечка, м3/час")]
    public float leakM3PerHour = 1.0f;
    [InspectorName("Скорость клапана")]
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
    [InspectorName("Название контура")]
    public string loopName = "Клавдиевый контур";
    [InspectorName("Объем системы, л")]
    public float systemVolumeL = 50f;
    [InspectorName("Длина контура, м")]
    public float loopLengthM = 25f;
    [InspectorName("Концентрация, %")]
    [Range(0f, 100f)] public float concentration = 10f;
    [InspectorName("Целевая концентрация, %")]
    [Range(0f, 100f)] public float targetConcentration = 10f;
    [InspectorName("Запас раствора, л")]
    public float solutionStockL = 100f;
    [InspectorName("Плотность раствора")]
    public float solutionDensity = 1.2f;
    [InspectorName("Запас кристаллов, кг")]
    public float crystalStockKg = 5.0f;
    [InspectorName("Скорость растворения, кг/мин")]
    public float dissolutionSpeedKgPerMinute = 0.5f;
    [InspectorName("Начальная температура")]
    public float initialTemperatureC = 20f;
    [InspectorName("Внешний подогрев, Вт")]
    public float externalHeatWatts = 0f;
    [InspectorName("Забирать тепло от двигателя")]
    public bool useEngineWasteHeat = true;
    [InspectorName("Теплопотери")]
    public float heatLoss = 0.05f;
    [InspectorName("Макс. давление, бар")]
    public float maxPressureBar = 200f;
    [InspectorName("КПД контура")]
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
