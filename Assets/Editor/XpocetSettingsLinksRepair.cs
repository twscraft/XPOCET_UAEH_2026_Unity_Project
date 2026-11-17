/* 
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/

using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class XpocetSettingsLinksRepair
{
    static string report;

    static void Check(bool ok, string message)
    {
        if (!ok)
            throw new Exception(message);
    }

    static T[] All<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(o => o.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    // Para controles que realmente son únicos en el panel.
    static T Only<T>(GameObject root) where T : Component
    {
        var controls = root.GetComponentsInChildren<T>(true);

        Check(
            controls.Length == 1,
            root.name + ": expected exactly one " +
            typeof(T).Name + ", found " + controls.Length
        );

        return controls[0];
    }

    // CORRECCIÓN:
    // Buscar únicamente el Toggle de pantalla completa.
    // Ignorar el Toggle interno del TMP_Dropdown.
    static Toggle FindFullscreenToggle(GameObject root)
    {
        var toggles = root
            .GetComponentsInChildren<Toggle>(true)
            .Where(t => t.gameObject.name ==
                        "TogglePantallaCompleta")
            .ToArray();

        Check(
            toggles.Length == 1,
            root.name +
            ": se esperaba exactamente un objeto " +
            "TogglePantallaCompleta, encontrados: " +
            toggles.Length
        );

        return toggles[0];
    }

    static void Save(UnityEngine.Object obj)
    {
        EditorUtility.SetDirty(obj);

        if (PrefabUtility.IsPartOfPrefabInstance(obj))
        {
            PrefabUtility
                .RecordPrefabInstancePropertyModifications(obj);
        }
    }

    static void Wire<T>(
        UnityEvent<T> ev,
        UnityAction<T> action)
    {
        // Rechazar callbacks inesperados.
        for (int i = 0;
             i < ev.GetPersistentEventCount();
             i++)
        {
            Check(
                ev.GetPersistentTarget(i) == null ||
                ev.GetPersistentTarget(i)
                    is AjustesPersistentes,

                "Unexpected settings callback: " +
                ev.GetPersistentMethodName(i)
            );
        }

        // Eliminar callbacks persistentes antiguos.
        for (int i = ev.GetPersistentEventCount() - 1;
             i >= 0;
             i--)
        {
            UnityEventTools.RemovePersistentListener(ev, i);
        }

        // Registrar callback dinámico.
        UnityEventTools.AddPersistentListener(ev, action);

        ev.SetPersistentListenerState(
            0,
            UnityEventCallState.RuntimeOnly
        );
    }

    static void VerifyDynamic(
        UnityEngine.Object control,
        string property)
    {
        var so = new SerializedObject(control);

        var calls = so.FindProperty(
            property + ".m_PersistentCalls.m_Calls"
        );

        Check(
            calls != null && calls.arraySize == 1,
            "Wrong callback count: " + control.name
        );

        var call = calls.GetArrayElementAtIndex(0);

        Check(
            call.FindPropertyRelative("m_Mode")
                .intValue == 0,

            "Callback must use dynamic argument: " +
            control.name
        );

        Check(
            call.FindPropertyRelative("m_Target")
                .objectReferenceValue != null,

            "Missing callback target: " + control.name
        );
    }

    [MenuItem(
        "Tools/XPOCET/Quality/Repair Visible Settings Links"
    )]
    public static void Repair()
    {
        Check(
            !EditorApplication.isPlayingOrWillChangePlaymode,
            "Exit Play first."
        );

        Check(
            !Enumerable.Range(0, SceneManager.sceneCount)
                .Any(i =>
                    SceneManager.GetSceneAt(i).isDirty),

            "Save open scenes first."
        );

        string directory = Path.GetFullPath(
            "QualityReports/Links-" +
            DateTime.Now.ToString("yyyyMMdd-HHmmss")
        );

        Directory.CreateDirectory(directory);

        report = Path.Combine(directory, "links.txt");

        // Respaldar archivos antes de modificarlos.
        foreach (var path in new[]
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/MainScene.unity",
            "ProjectSettings/EditorBuildSettings.asset"
        })
        {
            File.Copy(
                path,
                Path.Combine(
                    directory,
                    Path.GetFileName(path)
                )
            );
        }

        var setup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (string name in new[]
            {
                "MainMenu",
                "MainScene"
            })
            {
                var scene = EditorSceneManager.OpenScene(
                    "Assets/Scenes/" + name + ".unity"
                );

                // Localizar el panel visible de Ajustes.
                GameObject root = name == "MainMenu"
                    ? All<NavegacionMenu>(scene)
                        .Single(n => n.panelAjustes != null)
                        .panelAjustes

                    : All<PausarJuego>(scene)
                        .Single()
                        .panelAjustes;

                Check(
                    root != null,
                    "No visible settings panel: " + name
                );

                var panel =
                    root.GetComponent<AjustesPersistentes>();

                Check(
                    panel != null,
                    "No settings component on visible panel: " +
                    name
                );

                // Localizar los tres controles correctos.
                var slider = Only<Slider>(root);

                var dropdown = Only<TMP_Dropdown>(root);

                // CORREGIDO:
                // Ya no utilizamos Only<Toggle>(root).
                var toggle = FindFullscreenToggle(root);

                File.AppendAllText(
                    report,
                    name + " BEFORE: volume=" +
                    (panel.sliderVolumen == slider) +

                    ", quality=" +
                    (panel.dropdownCalidad == dropdown) +

                    ", fullscreen=" +
                    (panel.togglePantallaCompleta == toggle) +

                    Environment.NewLine
                );

                // Reparar referencias.
                panel.sliderVolumen = slider;
                panel.dropdownCalidad = dropdown;
                panel.togglePantallaCompleta = toggle;

                // Conectar eventos dinámicos.
                Wire<float>(
                    slider.onValueChanged,
                    panel.CambiarVolumen
                );

                Wire<int>(
                    dropdown.onValueChanged,
                    panel.CambiarCalidad
                );

                Wire<bool>(
                    toggle.onValueChanged,
                    panel.CambiarPantallaCompleta
                );

                // Actualizar niveles gráficos.
                dropdown.ClearOptions();

                dropdown.AddOptions(
                    QualitySettings.names.ToList()
                );

                dropdown.SetValueWithoutNotify(
                    QualitySettings.GetQualityLevel()
                );

                dropdown.RefreshShownValue();

                // Guardar cambios de componentes.
                Save(panel);
                Save(slider);
                Save(dropdown);
                Save(toggle);

                if (dropdown.captionText != null)
                    Save(dropdown.captionText);

                // Verificar callbacks Dynamic.
                VerifyDynamic(
                    slider,
                    "m_OnValueChanged"
                );

                VerifyDynamic(
                    dropdown,
                    "m_OnValueChanged"
                );

                VerifyDynamic(
                    toggle,
                    "onValueChanged"
                );

                // Verificar nombres de escenas.
                foreach (var nav in
                    All<NavegacionMenu>(scene))
                {
                    Check(
                        nav.escenaMenu == "MainMenu" &&
                        nav.escenaJuego == "MainScene",

                        "Wrong navigation scene names."
                    );
                }

                EditorSceneManager.MarkSceneDirty(scene);

                Check(
                    EditorSceneManager.SaveScene(scene),
                    "Scene save failed: " + name
                );

                File.AppendAllText(
                    report,
                    name +
                    " AFTER: visible controls assigned; " +
                    "all three callbacks dynamic " +
                    "and RuntimeOnly." +
                    Environment.NewLine
                );
            }

            // Eliminar rutas duplicadas en Build Settings.
            EditorBuildSettings.scenes =
                EditorBuildSettings.scenes
                    .GroupBy(s => s.path)
                    .Select(g =>
                        new EditorBuildSettingsScene(
                            g.Key,
                            g.Any(s => s.enabled)
                        )
                    )
                    .ToArray();

            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorSceneManager
                .RestoreSceneManagerSetup(setup);
        }

        Debug.Log(
            "Visible settings controls repaired " +
            "in MainMenu and MainScene. Report: " +
            report
        );
    }
}