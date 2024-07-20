﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IArmController
{
    public void control_arm(arm a);
    public bool arm_control_ended();
    public void draw_arm_control_gizmos();
}

/// <summary> Like a leg, but higher. </summary>
public class arm : MonoBehaviour, IArmController
{
    //##############//
    // Public state //
    //##############//

    public Transform shoulder;
    public Transform elbow;
    public Transform hand;
    public Transform elbow_bend_direction_override;

    public leg following;
    public float swing_multiplier = 3f;

    public Transform to_grab;
    public bool elbow_bends_backwards = false;

    public IArmController controller
    {
        get
        {
            if (_controller == null ||
                _controller.arm_control_ended())
                _controller = this;
            return _controller;
        }

        set => _controller = value;
    }
    IArmController _controller;

    public float total_length => bicep_length + forearm_length;
    public float extension => (hand.position - shoulder.position).magnitude / total_length;
    public float in_front_amount => Vector3.Dot(hand.position - shoulder.position, shoulder.forward) / total_length;

    //###############//
    // Private state //
    //###############//

    float bicep_length;
    float forearm_length;

    Transform initial_shoulder;
    Transform initial_elbow;

    //#########//
    // Methods //
    //#########//

    private void Update()
    {
        // Run the controller
        controller.control_arm(this);
    }

    public void control_arm(arm arm)
    {
        // Default control
        if (arm.to_grab == null) arm.update_with_leg();
        else arm.update_to_grab(arm.to_grab.position);
    }

    public bool arm_control_ended() { return false; }

    public void draw_arm_control_gizmos()
    {
        if (to_grab == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(to_grab.position, 0.025f);
    }

    private void Start()
    {
        // Get the shoulder-to-arm vector
        Vector3 whole_arm = hand.position - shoulder.position;

        // The direction the elbow sticks out
        Vector3 elbow_bend_dir = elbow.position - shoulder.position;
        elbow_bend_dir -= Vector3.Project(elbow_bend_dir, whole_arm);
        if (elbow_bend_direction_override != null)
            elbow_bend_dir = elbow_bend_direction_override.forward;

        Vector3 right = Vector3.Cross(elbow_bend_dir, -whole_arm);

        // Create a new shoulder object with
        // rotation so that down points towards elbow
        Transform shoulder_new = new GameObject("shoulder").transform;
        shoulder_new.position = shoulder.position;
        Vector3 shoulder_up = shoulder.position - elbow.position;
        Vector3 shoulder_forward = Vector3.Cross(right, shoulder_up);
        shoulder_new.rotation = Quaternion.LookRotation(shoulder_forward, shoulder_up);
        shoulder_new.SetParent(transform);
        shoulder.SetParent(shoulder_new);
        shoulder = shoulder_new;

        // Create a new elbow object with
        //  rotation so that down points towards hand
        Transform elbow_new = new GameObject("elbow").transform;
        elbow_new.position = elbow.position;
        Vector3 elbow_up = elbow.position - hand.position;
        Vector3 elbow_forward = Vector3.Cross(right, elbow_up);
        elbow_new.rotation = Quaternion.LookRotation(elbow_forward, elbow_up);
        elbow_new.SetParent(transform);
        elbow.SetParent(elbow_new);
        elbow = elbow_new;

        bicep_length = (elbow.position - shoulder.position).magnitude;
        forearm_length = (hand.position - elbow.position).magnitude;

        // Record the initial shoulder location + rotation
        initial_shoulder = new GameObject("initial_shoulder").transform;
        initial_shoulder.rotation = shoulder.rotation;
        initial_shoulder.position = shoulder.position;
        initial_shoulder.SetParent(transform);

        // Record the initial elbow location + rotation
        initial_elbow = new GameObject("initial_elbow").transform;
        initial_elbow.rotation = elbow.rotation;
        initial_elbow.position = elbow.position;
        initial_elbow.SetParent(transform);

        // Ensure parenting is correct
        hand.transform.SetParent(elbow);
    }

    /// <summary> The arm follows the leg, so it looks like we're running </summary>
    void update_with_leg()
    {
        if (following == null) return;

        float s = swing_multiplier *
            Mathf.Sin(following.progress * Mathf.PI) *
            following.step_length_boost;

        Vector3 shoulder_forward = initial_shoulder.forward + initial_shoulder.up * s / 2f;
        utils.rotate_towards(shoulder, Quaternion.LookRotation(shoulder_forward, initial_shoulder.up), Time.deltaTime * 360f);

        elbow.position = shoulder.position - shoulder.up * bicep_length;

        if (s < -0.5f) s = -0.5f;
        Vector3 elbow_forward = initial_elbow.forward + initial_elbow.up * s;
        utils.rotate_towards(elbow, Quaternion.LookRotation(elbow_forward, initial_elbow.up), Time.deltaTime * 360f);
    }

    /// <summary> The arm grabs grab_position. Maths is 
    /// simmilar to <see cref="leg.solve_leg"/> </summary>
    public void update_to_grab(Vector3 to_grab)
    {
        Vector3 dvec = to_grab - shoulder.position;
        float d = dvec.magnitude;

        Vector3 shoulder_elbow;
        Vector3 elbow_bend_dir = Vector3.Cross(initial_shoulder.right, dvec.normalized);

        float a = bicep_length;
        float b = forearm_length;

        if (d > a + b) // Overstretched
        {
            shoulder_elbow = dvec * a / (a + b);
        }
        else
        {
            // Work out lambda
            float lambda = d * d + b * b - a * a;

            if (Mathf.Abs(d) < 1e-4f) return;
            lambda = b * b - lambda * lambda / (4 * d * d);

            if (lambda < 0) return;
            lambda = Mathf.Sqrt(lambda);

            // Work out d1
            float d1 = a * a - lambda * lambda;

            if (d1 < 0) return;
            d1 = Mathf.Sqrt(d1);

            shoulder_elbow = d1 * dvec.normalized +
                lambda * elbow_bend_dir;
        }

        elbow.position = shoulder.position + shoulder_elbow;
        Vector3 right = Vector3.Cross(dvec, elbow_bend_dir);

        Vector3 shoulder_up = shoulder.position - elbow.position;
        Vector3 sholder_fw = Vector3.Cross(right, shoulder_up);
        shoulder.rotation = Quaternion.LookRotation(sholder_fw, shoulder_up);

        Vector3 elbow_up = elbow.position - to_grab;
        Vector3 elbow_fw = Vector3.Cross(right, elbow_up);
        elbow.rotation = Quaternion.LookRotation(elbow_fw, elbow_up);

    }

    public Vector3 nearest_in_reach(Vector3 target)
    {
        Vector3 dvec = target - shoulder.transform.position;
        if (dvec.magnitude > total_length)
            dvec = dvec.normalized * total_length;
        return dvec + shoulder.transform.position;
    }

    private void OnDrawGizmos()
    {
        controller?.draw_arm_control_gizmos();

        if (shoulder != null && elbow != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(shoulder.position, elbow.position);
        }

        if (elbow != null && hand != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(elbow.position, hand.position);
        }

        if (to_grab != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(to_grab.position, 0.05f);
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CanEditMultipleObjects()]
    [UnityEditor.CustomEditor(typeof(arm))]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (UnityEditor.EditorGUILayout.Toggle("auto setup", false))
            {
                var a = (arm)target;
                foreach (Transform t in a.transform)
                {
                    if (t.name.Contains("upper") || t.name.Contains("shoulder")) a.shoulder = t;
                    else if (t.name.Contains("lower") || t.name.Contains("elbow")) a.elbow = t;
                    else if (t.name.Contains("hand")) a.hand = t;
                }
            }
        }
    }
#endif

}
