using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShipPhysics))]
public class ShipPhysicsEditor : Editor
{
    SerializedProperty baseMassProp;
    SerializedProperty thrustForceProp;
    SerializedProperty turnTorqueProp;
    SerializedProperty liftForceProp;
    SerializedProperty airDensityProp;
    SerializedProperty dragCoefficientProp;
    SerializedProperty frontalAreaProp;
    
    SerializedProperty thrustInputProp;
    SerializedProperty turnInputProp;
    SerializedProperty liftInputProp;

    void OnEnable()
    {
        baseMassProp = serializedObject.FindProperty("baseMass");
        thrustForceProp = serializedObject.FindProperty("thrustForce");
        turnTorqueProp = serializedObject.FindProperty("turnTorque");
        liftForceProp = serializedObject.FindProperty("liftForce");
        
        airDensityProp = serializedObject.FindProperty("airDensity");
        dragCoefficientProp = serializedObject.FindProperty("dragCoefficient");
        frontalAreaProp = serializedObject.FindProperty("frontalArea");
        
        thrustInputProp = serializedObject.FindProperty("thrustInput");
        turnInputProp = serializedObject.FindProperty("turnInput");
        liftInputProp = serializedObject.FindProperty("liftInput");
    }

    public override void OnInspectorGUI()
    {
        // Получаем ссылку на сам скрипт корабля
        ShipPhysics ship = (ShipPhysics)target;
        Rigidbody rb = ship.GetComponent<Rigidbody>();

        // Обновляем значения из скрипта
        serializedObject.Update();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Параметры корабля", EditorStyles.boldLabel);
        
        // Отрисовка базовых параметров с принудительными русскими названиями
        EditorGUILayout.PropertyField(baseMassProp, new GUIContent("Стартовая масса (кг)"));
        EditorGUILayout.PropertyField(thrustForceProp, new GUIContent("Тяга маршевая (кгс)"));
        EditorGUILayout.PropertyField(turnTorqueProp, new GUIContent("Сила поворота (кгс*м)"));
        EditorGUILayout.PropertyField(liftForceProp, new GUIContent("Подъемная сила (кгс)"));
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Аэродинамика", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(airDensityProp, new GUIContent("Плотность воздуха"));
        EditorGUILayout.PropertyField(dragCoefficientProp, new GUIContent("Коэф. формы (Cd)"));
        EditorGUILayout.PropertyField(frontalAreaProp, new GUIContent("Лобовая площадь (м²)"));

        EditorGUILayout.HelpBox(
            "Памятка по коэф. формы (Cd):\n" +
            "• 0.04 : Идеальная обтекаемая капля\n" +
            "• 0.50 : Сфера / Округлый корпус\n" +
            "• 1.10 : Плоский торец (как контейнер)\n" +
            "• 2.00+ : Раскрытый парашют или ковш", 
            MessageType.None);

        // Расчет и вывод максимальной скорости
        float currentDrag = 0.5f * airDensityProp.floatValue * dragCoefficientProp.floatValue * frontalAreaProp.floatValue;
        float thrustNewtons = thrustForceProp.floatValue * Mathf.Abs(Physics.gravity.y);
        
        float maxSpeed = 0f;
        if (currentDrag > 0 && rb != null)
        {
            // На высокой скорости линейный демпфер равен 0, поэтому считаем только воздух
            maxSpeed = Mathf.Sqrt(thrustNewtons / currentDrag);
        }
        else if (currentDrag > 0) // Если Rigidbody вдруг нет
        {
            maxSpeed = Mathf.Sqrt(thrustNewtons / currentDrag);
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox($"Итоговое сопротивление воздуха: {currentDrag:F2}\nРасчетная макс. скорость: {maxSpeed:F1} м/с ({(maxSpeed * 3.6f):F0} км/ч)", MessageType.Info);

        EditorGUILayout.Space(15);
        
        // Отрисовка панели управления
        EditorGUILayout.LabelField("Панель управления рычагами", EditorStyles.boldLabel);
        
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.Slider(liftInputProp, -1f, 1f, new GUIContent("Подъем (вниз/вверх)"));
        EditorGUILayout.Slider(thrustInputProp, -1f, 1f, new GUIContent("Тяга (назад/вперед)"));
        EditorGUILayout.Slider(turnInputProp, -1f, 1f, new GUIContent("Руль (влево/вправо)"));

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Сбросить рычаги на 0", GUILayout.Height(30)))
        {
            liftInputProp.floatValue = 0f;
            thrustInputProp.floatValue = 0f;
            turnInputProp.floatValue = 0f;
        }

        EditorGUILayout.EndVertical();

        // Вывод телеметрии (скорости)
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Телеметрия", EditorStyles.boldLabel);
        
        GUIStyle telemetryStyle = new GUIStyle(GUI.skin.box);
        telemetryStyle.padding = new RectOffset(10, 10, 10, 10);
        EditorGUILayout.BeginVertical(telemetryStyle);
        
        if (rb != null)
        {
            EditorGUI.BeginChangeCheck();
            
            Vector3 newVelocity = EditorGUILayout.Vector3Field("Скорость (м/с)", rb.linearVelocity);
            Vector3 newAngularVelocity = EditorGUILayout.Vector3Field("Угловая скорость", rb.angularVelocity);
            
            if (EditorGUI.EndChangeCheck())
            {
                // Если мы ручками поменяли цифры в инспекторе, применяем их к Rigidbody
                rb.linearVelocity = newVelocity;
                rb.angularVelocity = newAngularVelocity;
            }
        }
        else
        {
            EditorGUILayout.LabelField("Ожидание компонента Rigidbody...");
        }

        EditorGUILayout.EndVertical();

        // Применяем изменения, это автоматом сохранит undo/redo и префабы
        serializedObject.ApplyModifiedProperties();
    }

    // Заставляем инспектор перерисовываться каждый кадр в режиме игры, 
    // чтобы цифры скорости бегали в реальном времени.
    public override bool RequiresConstantRepaint()
    {
        // Если пользователь кликнул в поле и вводит число, 
        // мы приостанавливаем перерисовку, чтобы его ввод не перезаписывался
        bool isTyping = GUIUtility.keyboardControl != 0;
        
        return Application.isPlaying && !isTyping;
    }
}
