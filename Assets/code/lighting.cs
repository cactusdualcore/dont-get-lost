﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class fog_distances
{
    public const float EXTRA_EXTREMELY_CLOSE = 2f;
    public const float EXTEREMELY_CLOSE = 6f;
    public const float VERY_CLOSE = 12f;
    public const float CLOSE = 32f;
    public const float MEDIUM = 80f;
    public const float FAR = 200f;
    public const float VERY_FAR = 400f;
    public const float OFF = 2000f;
}

public class lighting : MonoBehaviour
{
    public Light sun;
    public weather weather => weather.current;
    public Color day_sun_color => weather.day_sun_color;
    public Color dawn_sun_color => weather.dawn_sun_color;
    public Color dusk_sun_color => weather.dusk_sun_color;
    public Color night_sun_color => weather.night_sun_color;
    public float daytime_saturation => weather.daytime_saturation;
    public float nighttime_saturation => weather.nighttime_saturation;
    public float daytime_ambient_brightness => weather.daytime_ambient_brightness;
    public float nighttime_ambient_brightness => weather.nighttime_ambient_brightness;
    public Color daytime_ambient_color => weather.daytime_ambient_color;
    public Color nighttime_ambient_color => weather.nighttime_ambient_color;

    public static Color sky_color
    {
        get => sky.instance.color;
        set => sky.instance.color = value;
    }

    public static Color sky_color_daytime;
    public static float fog_distance;

    void Start()
    {
        // Allow static access
        manager = this;

        // Set the initial sun position + orientation
        sun.transform.position = Vector3.zero;
        sun.transform.LookAt(new Vector3(1, -2, 1));
    }

    static float saved_ambient_occlusion_intensity = -1;

    void Update()
    {
        // Work out the sun color from the time of day
        float day = time_manager.day_amount;
        float da = time_manager.dawn_amount;
        float ds = time_manager.dusk_amount;
        float nt = time_manager.night_amount;

        sun.color = new Color(
            day_sun_color.r * day + dawn_sun_color.r * da + dusk_sun_color.r * ds + night_sun_color.r * nt,
            day_sun_color.g * day + dawn_sun_color.g * da + dusk_sun_color.g * ds + night_sun_color.g * nt,
            day_sun_color.b * day + dawn_sun_color.b * da + dusk_sun_color.b * ds + night_sun_color.b * nt
        );

        // Sun moves in sky - looks kinda fast/does wierd things to the shadows
        if (options_menu.get_bool("moving_sun"))
            sun.transform.forward = new Vector3(
                Mathf.Cos(Mathf.PI * time_manager.time),
                -Mathf.Sin(Mathf.PI * time_manager.time) - 1.1f, // Always slightly downward
                0
            );
        else
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Overall ambient brightness
        float b = time_manager.time_to_brightness;

        // Set the ambient brightness according to the desired sky color
        UnityEngine.Rendering.HighDefinition.GradientSky sky;
        if (options_menu.global_volume.profile.TryGet(out sky))
        {
            // Work out ambient brightness as a function of raw brightness

            // Top brightness (low in daytime - mostly provided by sun)
            float tb = b * daytime_ambient_brightness * 0.015f + 0.04f * nighttime_ambient_brightness;

            // Middle brightness (medium in daytime, zero at night - otherwise water looks weird)
            float mb = b * daytime_ambient_brightness * 0.15f;

            // Bottom brightness (bright in daytime)
            float bb = b * daytime_ambient_brightness * 0.26f + 0.04f * nighttime_ambient_brightness;

            var c = daytime_ambient_color * b + nighttime_ambient_color * (1 - b);

            sky.top.value = new Color(c.r * tb, c.g * tb, c.b * tb);
            sky.middle.value = new Color(c.r * mb, c.g * mb, c.b * mb);
            sky.bottom.value = new Color(c.r * bb, c.g * bb, c.b * bb);
        }

        Color target_sky_color = sky_color_daytime;
        target_sky_color.r *= b;
        target_sky_color.g *= b;
        target_sky_color.b *= b;
        sky_color = Color.Lerp(sky_color, target_sky_color, Time.deltaTime * 5f);

        // Apply time-based color adjustments
        UnityEngine.Rendering.HighDefinition.ColorAdjustments color;
        if (options_menu.global_volume.profile.TryGet(out color))
        {
            // Reduce saturation at night
            float max_saturation = options_menu.get_float("saturation");
            max_saturation = (max_saturation + 100f) / 200f;
            float sat = max_saturation * (daytime_saturation * b + nighttime_saturation * (1 - b));
            color.saturation.value = sat * 200f - 100f;
        }

        // Enable/disable fog
        if (options_menu.get_bool("fog"))
        {
            UnityEngine.Rendering.HighDefinition.Fog fog;
            if (options_menu.global_volume.profile.TryGet(out fog))
            {
                // Disable fog in map view
                if (player.current != null)
                    fog.enabled.value = !player.current.map_open;

                // Keep fog color/distance up to date
                fog.baseHeight.value = world.SEA_LEVEL;
                fog.color.value = sky_color;
                fog.albedo.value = sky_color;
                fog.meanFreePath.value = Mathf.Lerp(
                    fog.meanFreePath.value, fog_distance, Time.deltaTime * 5f);
            }
        }

        // Keep ambient occlusion up-to-date
        UnityEngine.Rendering.HighDefinition.ScreenSpaceAmbientOcclusion ao;
        if (options_menu.global_volume.profile.TryGet(out ao))
        {
            if (player.current != null)
            {
                // Turn off ambient occlusion in map view
                if (saved_ambient_occlusion_intensity < 0)
                    saved_ambient_occlusion_intensity = ao.intensity.value;
                ao.intensity.value = player.current.map_open ?
                    0f : saved_ambient_occlusion_intensity;
            }
        }

        // Let the volume system know that it needs updating
        options_menu.global_volume.profile.isDirty = true;
    }

    //##################//
    // STATIC INTERFACE //
    //##################//

    static lighting manager;
}
