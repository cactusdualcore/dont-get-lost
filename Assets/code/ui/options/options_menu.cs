﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An option that can be changed from the options menu. </summary>
public abstract class options_menu_option : MonoBehaviour
{
    public int increase_graphics_priority = 0;
    public virtual bool reduce_graphics() { return false; }
    public virtual bool increase_graphics() { return false; }

    public abstract void load_default();
    public abstract void load_optimized();
    public abstract void initialize_option();
}

/// <summary> The options menu (essentially just a collection of 
/// options_menu_option children). </summary>
public class options_menu : MonoBehaviour
{
    public UnityEngine.UI.ScrollRect scroll_rect;
    public RectTransform main_menu;
    public UnityEngine.UI.Text render_range_text;

    /// <summary> Change the currenly open sub-menu. </summary>
    public void change_content(RectTransform content)
    {
        // Disable all content
        foreach (Transform t in scroll_rect.viewport.transform)
            t.gameObject.SetActive(false);

        // Enable the chosen content
        content.gameObject.SetActive(true);
        scroll_rect.content = content;
    }

    /// <summary> Set all the options to their default value. </summary>
    public void restore_default_options()
    {
        foreach (var o in GetComponentsInChildren<options_menu_option>(true))
            o.load_default();
    }

    /// <summary> Set all the options to their optimized value. </summary>
    public void restore_optimized_options()
    {
        foreach (var o in GetComponentsInChildren<options_menu_option>(true))
            o.load_optimized();
    }

    public void max_graphics()
    {
        foreach (var o in GetComponentsInChildren<options_menu_option>())
            while (o.increase_graphics()) { }
    }

    public void increase_graphics()
    {
        var options = new List<options_menu_option>(GetComponentsInChildren<options_menu_option>());
        options.Sort((a, b) => a.increase_graphics_priority.CompareTo(b.increase_graphics_priority));
        foreach (var o in options)
            if (o.increase_graphics())
                return;
    }

    public void reduce_graphics()
    {
        var options = new List<options_menu_option>(GetComponentsInChildren<options_menu_option>());
        options.Sort((a, b) => b.increase_graphics_priority.CompareTo(a.increase_graphics_priority));
        foreach (var o in options)
            if (o.reduce_graphics())
                return;
    }

    public void increase_render_range()
    {
        game.render_range_target += 10f;
    }

    public void decrease_render_range()
    {
        game.render_range_target -= 10f;
    }

    public void open_help()
    {
        player.current?.force_interaction(new player.help_book_interaction());
    }

    private void Update()
    {
        render_range_text.text = "Render range (" + Mathf.RoundToInt(game.render_range_target) + ")";
    }

    //##############//
    // STATIC STUFF //
    //##############//

    /// <summary> This is a useful thing to have access to here, 
    /// as many graphics settings are controlled by this. </summary>
    public static UnityEngine.Rendering.Volume global_volume
    {
        get
        {
            if (_global_volume == null)
                _global_volume = FindObjectOfType<UnityEngine.Rendering.Volume>();
            return _global_volume;
        }
    }
    static UnityEngine.Rendering.Volume _global_volume;

    /// <summary> Called when the player clicks on "save and quit". </summary>
    public void save_and_quit()
    {
        game.save_and_quit();
    }

    /// <summary> Called to initialize the options from their saved values.
    /// Note that this might be called before the menu is open, so 
    /// <see cref="options_menu_option.initialize_option"/> should
    /// not depend on the UI being loaded. </summary>
    public static void initialize()
    {
        var om = Resources.Load<options_menu>("ui/options_menu");
        foreach (var o in om.GetComponentsInChildren<options_menu_option>(true))
            o.initialize_option();
    }

    /// <summary> Set a floating point option. </summary>
    public static void set_float(string name, float val)
    {
        UnityEngine.Rendering.HighDefinition.ColorAdjustments color_adjust;
        if (!global_volume.profile.TryGet(out color_adjust))
            throw new System.Exception("Global volume has no color adjustments override!");

        switch (name)
        {
            case "contrast":
                color_adjust.contrast.overrideState = true;
                color_adjust.contrast.value = Mathf.Clamp(val, -100f, 100f);
                break;

            case "saturation":
                color_adjust.saturation.overrideState = true;
                color_adjust.saturation.value = Mathf.Clamp(val, -100f, 100f);
                break;

            case "brightness":
                color_adjust.postExposure.overrideState = true;
                color_adjust.postExposure.value = Mathf.Clamp(val, -5f, 5f);
                break;

            case "master_volume":
                AudioListener.volume = val;
                break;

            case "music_volume":
                foreach (var mg in FindObjectsOfType<music_generator>())
                    mg.volume = val;
                break;

            case "field_of_view":
                player.call_when_current_player_available(() =>
                {
                    foreach (var c in player.current.camera.GetComponentsInChildren<Camera>())
                        c.fieldOfView = val;
                });
                break;

            case "mouse_look_sensitivity":
                controls.mouse_look_sensitivity = val;
                break;

            default:
                Debug.LogError("Unkown float option: " + name);
                return;
        }

        PlayerPrefs.SetFloat(name, val);
    }

    public static float get_float(string name)
    {
        return PlayerPrefs.GetFloat(name);
    }

    /// <summary> Set a boolean option. </summary>
    public static void set_bool(string name, bool value)
    {
        UnityEngine.Rendering.HighDefinition.Exposure exposure;
        if (!global_volume.profile.TryGet(out exposure))
            throw new System.Exception("Global volume has no exposure override!");

        UnityEngine.Rendering.HighDefinition.Fog fog;
        if (!global_volume.profile.TryGet(out fog))
            throw new System.Exception("Global volume has no fog override!");

        UnityEngine.Rendering.HighDefinition.ScreenSpaceAmbientOcclusion ao;
        if (!global_volume.profile.TryGet(out ao))
            throw new System.Exception("Global volume has no ambient occlusion override!");

        switch (name)
        {
            case "vsync":
                QualitySettings.vSyncCount = value ? 1 : 0;
                Application.targetFrameRate = value ? -1 : game.MAX_FRAMERATE;
                break;

            case "water_reflections":
                water_reflections.reflections_enabled = value;
                break;

            case "auto_exposure":
                if (value)
                    exposure.mode.value = UnityEngine.Rendering.HighDefinition.ExposureMode.Automatic;
                else
                    exposure.mode.value = UnityEngine.Rendering.HighDefinition.ExposureMode.Fixed;
                break;

            case "fog":
                fog.enabled.value = value;
                break;

            case "volumetric_fog":
                fog.enableVolumetricFog.value = value;

                // Attempt to match the apparent thickness of 
                // volumetric and non-volumetric fog
                fog.maximumHeight.value = value ? 80 : 50;
                break;

            case "moving_sun":
                break;

            case "physical_sky":
                sky.instance.gameObject.SetActive(value);
                break;

            case "clip_out_of_range":
                chunk.on_clip_setting_change(value);
                break;

            case "ambient_occlusion":
                ao.active = value;
                break;

            case "shadows":
                var light_data = FindObjectOfType<lighting>().sun.GetComponent<
                    UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                light_data.EnableShadows(value);
                break;

            case "key_rotation":
                controls.key_based_building = value;
                break;

            case "invert_mouse_y":
                controls.invert_mouse_y = value;
                break;

            case "unlimited_map_zoom":
                controls.unlimted_map_zoom = value;
                break;

            case "arachnophobia_mode":
                break;

            case "motion_sickness_mode":
                break;

            default:
                Debug.LogError("Unkown bool option: " + name);
                return;
        }

        PlayerPrefs.SetInt(name, value ? 1 : 0);
    }

    public static bool get_bool(string name) => PlayerPrefs.GetInt(name) > 0;

    /// <summary> Is the options menu currently open? </summary>
    public static bool open
    {
        get => _menu != null;
        set
        {
            if (_menu == null && value)
            {
                _menu = Resources.Load<options_menu>("ui/options_menu").inst(game.canvas.transform);
                _menu.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                _menu.change_content(_menu.main_menu);
            }
            else if (_menu != null && !value)
            {
                Destroy(_menu.gameObject);
                _menu = null;
            }
        }
    }
    static options_menu _menu; // The actual menu object
}
