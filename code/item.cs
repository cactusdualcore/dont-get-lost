﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item : interactable
{
    const float CARRY_RESTORE_FORCE = 10f;
    public const float WELD_RANGE = 5f;

    // Component access
    new public Rigidbody rigidbody { get; private set; }
    snap_point[] snap_points { get { return GetComponentsInChildren<snap_point>(); } }

    // A weld represents the fixture of an item via a pivot
    // to a particular weld point in space
    public class weld_info
    {
        item to_weld;
        snap_point pivot;
        Vector3 weld_location;
        Quaternion weld_rotation;
        Quaternion target_pivot_rotation;

        void set_pivot_rotation(Quaternion rotation)
        {
            to_weld.transform.rotation = rotation;
            to_weld.transform.rotation *= Quaternion.Inverse(pivot.transform.localRotation);

            Vector3 disp = weld_location - pivot.transform.position;
            to_weld.transform.position += disp;
        }

        public weld_info(
            item to_weld,
            snap_point pivot,
            Vector3 weld_location,
            Quaternion weld_rotation
            )
        {
            this.to_weld = to_weld;
            this.pivot = pivot;
            this.weld_location = weld_location;
            this.weld_rotation = weld_rotation;

            // Allign the pivot to be antiparrallel to the weld rotation
            Vector3 weld_up = weld_rotation * Vector3.up;
            Vector3 weld_forward = weld_rotation * Vector3.forward;
            target_pivot_rotation = Quaternion.LookRotation(weld_forward, -weld_up);
            set_pivot_rotation(target_pivot_rotation);
        }

        Vector3[] axes()
        {
            return new Vector3[]
            {
                new Vector3(1,0,0),
                new Vector3(0,1,0),
                new Vector3(0,0,1),
                new Vector3(-1,0,0),
                new Vector3(0,-1,0),
                new Vector3(0,0,-1)
            };
        }

        Vector3[] snap_axes()
        {
            Vector3[] axes = new Vector3[]
            {
                new Vector3(1,0,0),
                new Vector3(0,1,0),
                new Vector3(0,0,1),

                new Vector3(-1,0,0),
                new Vector3(0,-1,0),
                new Vector3(0,0,-1),

                new Vector3(0,1,1),
                new Vector3(1,0,1),
                new Vector3(1,1,0),

                new Vector3(0,-1,1),
                new Vector3(-1,0,1),
                new Vector3(-1,1,0),

                new Vector3(0,1,-1),
                new Vector3(1,0,-1),
                new Vector3(1,-1,0),

                new Vector3(0,-1,-1),
                new Vector3(-1,0,-1),
                new Vector3(-1,-1,0),
            };

            for (int i = 0; i < axes.Length; ++i)
                axes[i] = weld_rotation * axes[i].normalized;

            return axes;
        }

        void key_rotate()
        {
            Vector3 rd = utils.find_to_min(axes(), (a) => -Vector3.Dot(a, player.current.camera.transform.right));
            Vector3 fd = utils.find_to_min(axes(), (a) => -Vector3.Dot(a, player.current.camera.transform.forward));

            if (Input.GetKeyDown(KeyCode.D))
                to_weld.transform.RotateAround(pivot.transform.position, fd, -45);
            if (Input.GetKeyDown(KeyCode.A))
                to_weld.transform.RotateAround(pivot.transform.position, fd, 45);
        }

        public void rotate(float x, float y)
        {
            key_rotate();

            // The mouse movement in the plane of the camera view
            Vector3 mouse_dir = player.current.camera.transform.right * x +
                                player.current.camera.transform.up * y;

            canvas.set_direction_indicator(new Vector2(x, y));

            if (mouse_dir.magnitude < 5) return;

            // Find the axis most alligned with the mouse movement 
            // (ignoring component in forward direction)
            Vector3 axis = utils.find_to_min(snap_axes(), (a) =>
            {
                a -= Vector3.Project(a, player.current.camera.transform.forward);
                return Vector3.Dot(mouse_dir.normalized, a.normalized);
            });

            set_pivot_rotation(Quaternion.LookRotation(pivot.transform.right, axis));
        }

        public void draw_gizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(
                pivot.transform.position,
                pivot.transform.position + pivot.transform.up);

            Gizmos.color = Color.red;
            Vector3 d = Vector3.one / 100f;
            Gizmos.DrawLine(weld_location + d, weld_location + d + weld_rotation * Vector3.up);
        }
    }

    // The current weld
    weld_info _weld;
    public weld_info weld
    {
        get { return _weld; }
        set
        {
            _weld = value;
            if (value == null)
            {
                rigidbody.isKinematic = false;
                return;
            }

            rigidbody.isKinematic = true;
        }
    }

    snap_point closest_to_ray(Ray ray)
    {
        snap_point ret = null;

        // Attempt to raycast to this item/find the nearest
        // snap_point to the raycast hit
        RaycastHit hit;
        if (utils.raycast_for_closest<item>(
            ray, out hit, WELD_RANGE,
            (t) => t == this))
        {
            // Find the nearest snap point to the hit
            float min_dis_pt = float.MaxValue;
            foreach (var s in snap_points)
            {
                float dis_pt = (s.transform.position - hit.point).sqrMagnitude;
                if (dis_pt < min_dis_pt)
                {
                    min_dis_pt = dis_pt;
                    ret = s;
                }
            }
        }

        if (ret != null)
            return ret;

        // Just find the nearest snap point to the ray
        float min_dis = float.MaxValue;
        foreach (var sp in snap_points)
        {
            Vector3 to_line = sp.transform.position - ray.origin;
            to_line -= Vector3.Project(to_line, ray.direction);
            float dis = to_line.sqrMagnitude;
            if (dis < min_dis)
            {
                min_dis = dis;
                ret = sp;
            }
        }

        return ret;
    }

    void fix_to(item other)
    {
        snap_point snap_from = this.closest_to_ray(player.current.camera_ray());
        snap_point snap_to = other.closest_to_ray(player.current.camera_ray());

        if (snap_from == null) return;
        if (snap_to == null) return;

        weld = new weld_info(this,
            snap_from,
            snap_to.transform.position,
            Quaternion.identity);
    }

    void fix_at(RaycastHit hit)
    {
        snap_point snap_from = this.closest_to_ray(player.current.camera_ray());

        if (snap_from == null) return;

        weld = new weld_info(this,
            snap_from,
            hit.point,
            Quaternion.identity);
    }

    public override FLAGS player_interact()
    {
        // Drop item
        if (Input.GetMouseButtonDown(0))
        {
            stop_interaction();
            return FLAGS.NONE;
        }

        if (weld != null)
        {
            // Unweld on right click
            if (Input.GetMouseButtonDown(1))
                weld = null;
            else
            {
                weld.rotate(5 * Input.GetAxis("Mouse X"),
                            5 * Input.GetAxis("Mouse Y"));
                return FLAGS.DISALLOWS_ROTATION | FLAGS.DISALLOWS_MOVEMENT;
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            // Find an item to snap to
            RaycastHit hit;
            item other = utils.raycast_for_closest<item>(
                player.current.camera_ray(), out hit,
                WELD_RANGE, (t) => t != this);

            if (other != null)
                fix_to(other);
            else
            {
                // Raycast for a surface to weld to
                Component closest = utils.raycast_for_closest<Component>(
                    player.current.camera_ray(), out hit,
                    WELD_RANGE, (c) => !c.transform.IsChildOf(transform));

                if (closest != null)
                    fix_at(hit);
            }
        }

        Vector3 carry_point = player.current.camera.transform.position +
            carry_distance * player.current.camera.transform.forward;

        Vector3 dx = carry_pivot.position - carry_point;
        Vector3 v = rigidbody.velocity;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0) carry_distance *= 1.2f;
        else if (scroll < 0) carry_distance /= 1.2f;
        carry_distance = Mathf.Clamp(carry_distance, 1.0f, player.INTERACTION_RANGE);

        rigidbody.AddForce(-CARRY_RESTORE_FORCE * (dx + v));

        return FLAGS.NONE;
    }

    Transform carry_pivot;
    float carry_distance;

    public override void on_start_interaction(RaycastHit point_hit)
    {
        carry_pivot = new GameObject("pivot").transform;
        carry_pivot.SetParent(transform);
        carry_pivot.transform.position = point_hit.point;
        carry_pivot.rotation = player.current.camera.transform.rotation;

        transform.SetParent(player.current.camera.transform);
        weld = null;
        rigidbody.useGravity = false;
        rigidbody.angularDrag *= 200f;
        carry_distance = 2f;
    }

    public override void on_end_interaction()
    {
        transform.SetParent(null);
        rigidbody.useGravity = true;
        rigidbody.angularDrag /= 200f;
        Destroy(carry_pivot.gameObject);
    }

    // Return a cursor that looks like grabbing if we are carrying an item
    public override string cursor()
    {
        return cursors.GRAB_CLOSED;
    }

    private void OnDrawGizmos()
    {
        if (weld != null) weld.draw_gizmos();
    }

    //----------------//
    //  STATIC STUFF  //
    //----------------//

    public static item spawn(string name, Vector3 position)
    {
        var i = Resources.Load<item>("items/" + name).inst();
        i.transform.position = position;
        i.rigidbody = i.gameObject.AddComponent<Rigidbody>();
        i.rigidbody.velocity = Random.onUnitSphere;
        i.transform.Rotate(0, Random.Range(0, 360f), 0);
        return i;
    }
}