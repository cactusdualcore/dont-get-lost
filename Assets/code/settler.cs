﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler : character, IInspectable
{
    public const float HUNGER_PER_SECOND = 0.2f;
    public const float TIREDNESS_PER_SECOND = 100f / time_manager.DAY_LENGTH;

    public networked_variables.net_int hunger;
    public networked_variables.net_int tiredness;

    public override float position_resolution()
    {
        return 0.1f;
    }

    public override float position_lerp_speed()
    {
        return 2f;
    }

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();

        hunger = new networked_variables.net_int(min_value: 0, max_value: 100);
        tiredness = new networked_variables.net_int(min_value: 0, max_value: 100);
    }

    public override bool persistant()
    {
        return true;
    }

    protected override ICharacterController default_controller()
    {
        return new settler_control();
    }

    new public string inspect_info()
    {
        return "Settler\n" +
               "    " + Mathf.Round(hunger.value) + "% hungry\n" +
               "    " + Mathf.Round(tiredness.value) + "% tired";
    }
}

class settler_control : ICharacterController
{
    List<settler_path_element> path;
    settler_interactable target;
    float walk_speed_mod = 1f;
    bool interaction_complete = false;
    float interaction_time = 0;

    float delta_hunger = 0;
    float delta_tired = 0;

    public void control(character c)
    {
        var s = (settler)c;

        // Get hungry/tired
        delta_hunger += settler.HUNGER_PER_SECOND * Time.deltaTime;
        delta_tired += settler.TIREDNESS_PER_SECOND * Time.deltaTime;

        if (delta_hunger > 1f)
        {
            delta_hunger = 0f;
            s.hunger.value += 1;
        }

        if (delta_tired > 1f)
        {
            delta_tired = 0f;
            s.tiredness.value += 1;
        }

        if (target == null)
        {
            // Set the initial target to the nearest one and teleport there
            target = settler_interactable.nearest(settler_interactable.TYPE.WORK, c.transform.position);
            if (target == null || target.path_element == null) return;
            c.transform.position = target.path_element.transform.position;
        }

        if (path != null && path.Count > 0)
        {
            if (path[0] == null)
            {
                // Path has been destroyed, reset
                path = null;
                target = null;
                return;
            }

            // Walk the path to completion  
            Vector3 next_point = path[0].transform.position;
            Vector3 forward = next_point - c.transform.position;
            forward.y = 0;
            if (forward.magnitude > 10e-3f) c.transform.forward = forward;

            if (utils.move_towards(c.transform, next_point,
                Time.deltaTime * c.walk_speed * walk_speed_mod))
                path.RemoveAt(0);

            return;
        }

        if (interaction_complete || target.interact(s, interaction_time))
        {
            interaction_complete = true;

            // The next candidate interaction
            settler_interactable next = null;
            if (Random.Range(0, 100) < s.hunger.value)
                next = settler_interactable.random(settler_interactable.TYPE.EAT);

            else if (Random.Range(0, 100) < s.tiredness.value)
                next = settler_interactable.random(settler_interactable.TYPE.SLEEP);

            // Didn't need to do anything, so get to work
            if (next == null)
                next = settler_interactable.random(settler_interactable.TYPE.WORK);

            // Don't consider null interactables
            // or returning to the same target
            if (next == null) return;
            if (next == target) return;

            // Attempt to path to new target
            path = settler_path_element.path(target.path_element, next.path_element);

            if (path == null)
            {
                // Pathing failed
                return;
            }

            // Pathing success, this is our next target
            target = next;

            // Reset things
            interaction_complete = false;
            interaction_time = 0;

            // Set a new randomized path speed, so settlers 
            // don't end up 100% in-phase
            walk_speed_mod = Random.Range(0.9f, 1.1f);
        }
        else
        {
            // Increment the interaction timer
            interaction_time += Time.deltaTime;
        }
    }

    public void draw_gizmos()
    {
        Gizmos.color = Color.green;

        if (target != null)
            Gizmos.DrawWireSphere(target.transform.position, 0.2f);

        if (path != null)
            for (int i = 1; i < path.Count; ++i)
                Gizmos.DrawLine(
                    path[i].transform.position + Vector3.up,
                    path[i - 1].transform.position + Vector3.up);
    }

    public void draw_inspector_gui()
    {
#if UNITY_EDITOR
        string text = target == null ? "No target\n" : "Target = " + target.name + "\n";
        text += (path == null) ? "No path\n" : "Path length = " + path.Count;
        UnityEditor.EditorGUILayout.TextArea(text);
#endif
    }
}
