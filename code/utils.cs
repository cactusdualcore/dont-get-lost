﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class utils
{
    // Create an exact copy of the object t
    public static T inst<T>(this T t) where T : Object
    {
        var ret = Object.Instantiate(t);
        ret.name = t.name;
        return ret;
    }

    // Returns true if i is within size range
    public static bool in_range(int i, int size)
    {
        return (i >= 0) && (i < size);
    }

    // Rounds a float so 0.5 -> 1.0
    public static int round(float f)
    {
        int ret = Mathf.FloorToInt(f);
        f -= ret;
        if (f < 0.5f) return ret;
        return ret + 1;
    }

    // Get the sign of f (returning 0 if f is 0)
    public static int sign(float f)
    {
        if (f < 0) return -1;
        if (f > 0) return 1;
        return 0;
    }

    // Log something to file
    static HashSet<string> opened = new HashSet<string>();
    public static void log(string s, string logfile)
    {
        string filename = Application.persistentDataPath + "/" + logfile + ".log";

        if (!opened.Contains(filename))
        {
            System.IO.File.Delete(filename);
            opened.Add(filename);
        }

        using (var stream = System.IO.File.AppendText(filename))
        {
            stream.Write(s + "\n");
            stream.Flush();
        }
    }

    // Raycast for the nearest object of the given type
    public delegate bool accept_func<T>(T t);
    public static T raycast_for_closest<T>(Ray ray, out RaycastHit hit,
        float max_distance = float.MaxValue, accept_func<T> accept = null)
        where T : Component
    {
        float min_dis = float.MaxValue;
        hit = new RaycastHit();
        T ret = default;

        foreach (var h in Physics.RaycastAll(ray, max_distance))
        {
            var t = h.collider.gameObject.GetComponentInParent<T>();
            if (t != null)
            {
                if (accept != null)
                    if (!accept(t))
                        continue;

                float dis = (ray.origin - h.point).sqrMagnitude;
                if (dis < min_dis)
                {
                    min_dis = dis;
                    hit = h;
                    ret = t;
                }
            }
        }

        return ret;
    }

    // Find the object in to_search that minimizes the given function
    public delegate float float_func<T>(T t);
    public static T find_to_min<T>(IEnumerable<T> to_search, float_func<T> objective)
    {
        T ret = default;
        float min = float.MaxValue;
        foreach (var t in to_search)
        {
            float val = objective(t);
            if (val < min)
            {
                min = val;
                ret = t;
            }
        }
        return ret;
    }

    // Check if the given circle intersects the given square
    public static bool circle_intersects_square(
        Vector2 circle_centre, float radius,
        Vector2 square_centre, float sq_width, float sq_height)
    {
        float dx = Mathf.Abs(circle_centre.x - square_centre.x);
        float dy = Mathf.Abs(circle_centre.y - square_centre.y);

        if (dx > sq_width / 2 + radius) return false;
        if (dy > sq_height / 2 + radius) return false;

        if (dx < sq_width / 2) return true;
        if (dy < sq_height / 2) return true;

        float corner_distance_sq = (dx - sq_width / 2) * (dx - sq_width / 2) +
                                   (dy - sq_height / 2) * (dy - sq_height / 2);

        return corner_distance_sq < radius * radius;
    }
}