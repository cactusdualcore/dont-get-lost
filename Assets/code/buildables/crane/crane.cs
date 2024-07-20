﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class crane : MonoBehaviour, IPlayerInteractable
{
    public float rotation_speed = 45f;
    public float winch_speed = 1f;

    public Transform hook;
    public Transform extender;
    public Transform extender_bottom;

    /// <summary> Called when the hook arrives at (or at least, is 
    /// as close as possible to) <see cref="target"/>. </summary>
    public arrive_func on_arrive;
    public delegate void arrive_func();

    /// <summary> The desired location for the hook. </summary>
    public Vector3 target
    {
        get => _target;
        set
        {
            // New target - first step towards
            // getting there is to retract
            _target = value;
            state = STATE.RETRACTING;
        }
    }
    Vector3 _target;

    Transform pivot;
    float hook_reset_y;
    float init_extension;

    float extension => Mathf.Abs((extender_bottom.position - extender.position).y);

    public enum STATE
    {
        ARRIVED,
        RETRACTING,
        ROTATING,
        EXTENDING
    }
    public STATE state { get; private set; }

    private void Start()
    {
        hook_reset_y = hook.position.y;
        init_extension = extension;

        // Setup a pivot to allow easy control of the cranes rotation
        Vector3 pivot_to_hook = hook.position - transform.position;
        pivot_to_hook.y = 0;
        pivot = new GameObject("pivot").transform;
        pivot.transform.SetParent(transform.parent);
        pivot.position = transform.position;
        pivot.rotation = Quaternion.LookRotation(pivot_to_hook.normalized, Vector3.up);
        pivot.localScale = Vector3.one;
        transform.SetParent(pivot);

        // Setup extender to allow easy control of it's length
        Transform new_extender = new GameObject("extender").transform;
        new_extender.transform.SetParent(extender.parent);
        new_extender.transform.position = extender.position;
        new_extender.transform.rotation = pivot.rotation;
        new_extender.transform.localScale = Vector3.one;
        extender.transform.SetParent(new_extender);
        extender = new_extender;
    }

    private void Update()
    {
        switch (state)
        {
            case STATE.ARRIVED:

                // Call the arrive function, and reset
                if (on_arrive != null)
                {
                    // Temporary variable needed, in case
                    // on_arrive is set from on_arrive
                    var tmp = on_arrive;
                    on_arrive = null;
                    tmp();
                }
                return;

            case STATE.RETRACTING:

                // Retract hook
                Vector3 hook_target = hook.transform.position;
                hook_target.y = hook_reset_y;
                if (utils.move_towards(hook, hook_target, Time.deltaTime * winch_speed))
                    state = STATE.ROTATING;

                Vector3 extender_scale = extender.localScale;
                extender_scale.y = extension / init_extension;
                extender.localScale = extender_scale;
                return;

            case STATE.ROTATING:

                // Rotate towards target
                Vector3 delta_xz = target - pivot.position;
                delta_xz.y = 0;
                delta_xz.Normalize();

                Quaternion target_rot = Quaternion.LookRotation(delta_xz, Vector3.up);
                if (utils.rotate_towards(pivot, target_rot, Time.deltaTime * rotation_speed))
                    state = STATE.EXTENDING;
                return;

            case STATE.EXTENDING:

                // Extend hook
                hook_target = hook.transform.position;
                hook_target.y = target.y;
                if (utils.move_towards(hook, hook_target, Time.deltaTime * winch_speed))
                    state = STATE.ARRIVED;

                extender_scale = extender.localScale;
                extender_scale.y = extension / init_extension;
                extender.localScale = extender_scale;
                return;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target, 0.1f);
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        return new player_interaction[] { new player_inspectable(transform)
        {
            text = () => "Crane (" + state.ToString().ToLower() + ")"
        }};
    }
}
