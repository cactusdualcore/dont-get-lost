﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A downhill track for items, represented 
/// as an extended item node. </summary>
public class item_gutter : item_node
{
    public override string node_description(int item_count) => item_count + " items on gutter";

    public const float ITEM_SEPERATION = item.LOGISTICS_SIZE;

    public Transform start;
    public Transform end;

    public override Vector3 output_point => end.position;
    public override Vector3 input_point(Vector3 input_from)
    {
        // Work out the nearest point to the downwards line
        // from input_from on the line from start to end
        // I found the maths for this surprisingly difficult
        // so there is probably a better/faster way to do this

        // Vector from input to end of line
        Vector3 to_end = end.position - input_from;

        // Vector from start of line to end of line
        Vector3 line = end.position - start.position;
        Vector3 along_line = line.normalized;

        // Down
        Vector3 down = Vector3.down;

        // The direction perpendicular to the line and to down
        Vector3 normal = Vector3.Cross(down, along_line);

        // An intermediate point, above the target point
        Vector3 inter = input_from + Vector3.Project(to_end, normal);

        // The direction from start to end, in the x-z plane
        Vector3 in_plane = along_line;
        in_plane.y = 0;
        in_plane.Normalize();

        // Vector to end of line from intermediate point
        Vector3 inter_to_end = end.position - inter;

        // The height of the result above the end of the line
        float h = -line.y * Vector3.Dot(inter_to_end, in_plane) / Vector3.Dot(line, in_plane);

        inter.y = end.position.y + h;
        return inter;
    }

    protected override bool can_input_from(item_node other)
    {
        // Can't link destroyed things
        if (this == null || other == null) return false;

        // Can't link to perfectly vertical gutters
        if (is_vertical) return false;

        Vector3 input_point = this.input_point(other.output_point);
        Vector3 delta = input_point - other.output_point;

        // Don't allow uphill links
        if (delta.y > UPHILL_LINK_ALLOW) return false;

        // Check input point is close enough to directly below the other
        delta.y = 0;
        if (delta.magnitude > LINK_DISTANCE_TOLERANCE) return false;

        // Check input position is between start and end (to within tolerance)
        Vector3 start_to_end = end.position - start.position;
        float distance_along = Vector3.Dot(input_point - start.position, start_to_end.normalized);
        if (distance_along < -LINK_DISTANCE_TOLERANCE) return false;
        if (distance_along > start_to_end.magnitude + LINK_DISTANCE_TOLERANCE) return false;

        // Check there is nothing in the way
        Vector3 out_to_in = input_point - other.output_point;
        foreach (var h in Physics.RaycastAll(other.output_point,
            out_to_in.normalized, out_to_in.magnitude))
        {
            // Ignore collisions with self/other/ignore_logistics_collisions_with things
            if (!ignore_logistics_collisions_with(h, building?.transform, other.building?.transform))
                return false;
        }

        return true;
    }

    bool is_vertical
    {
        get
        {
            Vector3 along = (end.position - start.position).normalized;
            return new Vector2(along.x, along.z).magnitude < 1e-4f;
        }
    }

    protected override bool can_output_to(item_node other)
    {
        return true;
    }

    protected override void postprocess_connections(List<item_node> outputs_to, List<item_node> inputs_from,
        out HashSet<item_node> outputs_to_remove, out HashSet<item_node> inputs_to_remove)
    {
        outputs_to_remove = new HashSet<item_node>();
        inputs_to_remove = new HashSet<item_node>();

        // If this gutter outputs to something that is connected very
        // closely, then don't output to anything else. This deals with
        // the problem of a row of gutters above another row dropping through.
        // 
        //  |----------|----------|----------|--> Gutter 1 -->
        //                        |
        //                        |  <--- Stops this drop from happening
        //                        |
        //  |----------|----------|----------|--> Gutter 2 -->
        //
        item_node exclusive_output = null;
        foreach (var o in outputs_to)
            if ((output_point - o.input_point(output_point)).magnitude < 0.01f)
            {
                exclusive_output = o;
                break;
            }

        // No exclusive output identified
        if (exclusive_output == null)
            return;

        // Remove all outputs, except the one flagged as exclusive above
        foreach (var o in outputs_to)
            if (o != exclusive_output)
                outputs_to_remove.Add(o);
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(start.position, end.position);
    }

    protected override void Start()
    {
        // Switch start/end if they aren't going downhill
        if (start.position.y < end.position.y)
        {
            Transform tmp = start;
            start = end;
            end = tmp;
        }

        base.Start();
    }

    private void Update()
    {
        if (this == null)
            return; // Destroyed

        // Allign items to gutter
        for (int i = 0; i < item_count; ++i)
            get_item(i).transform.forward = end.position - start.position;

        for (int i = 1; i < item_count; ++i)
        {
            item b = get_item(i - 1);
            item a = get_item(i);

            // Get direction towards next item
            Vector3 delta = b.transform.position - a.transform.position;

            // Only move towards the next
            // item if we're far enough apart
            if (delta.magnitude > ITEM_SEPERATION)
            {
                // Move up to ITEM_SEPERATION away from the next item
                delta = delta.normalized * (delta.magnitude - ITEM_SEPERATION);
                float max_move = Time.deltaTime;
                if (delta.magnitude > max_move)
                    delta = delta.normalized * max_move;
                a.transform.position += delta;
            }
        }

        if (item_count > 0)
        {
            var itm = get_item(0);

            // Move first item towards output, dropping it off the end
            if (utils.move_towards(itm.transform, end.position, Time.deltaTime))
                item_dropper.create(release_item(0), end.position, next_output());
        }
    }

    //#########//
    // DISPLAY //
    //#########//

    GameObject display;

    protected override bool is_display_enabled()
    {
        return base.is_display_enabled() && display != null && display.activeInHierarchy;
    }

    protected override void set_display(bool enabled)
    {
        base.set_display(enabled);

        if (display == null)
        {
            display = new GameObject("display");
            display.transform.SetParent(transform);
            display.transform.position = (end.position + start.position) / 2f;
            display.transform.forward = end.position - start.position;

            var path = Resources.Load<GameObject>("misc/gutter_path").inst();
            path.transform.SetParent(display.transform);
            path.transform.localPosition = Vector3.zero;
            path.transform.localRotation = Quaternion.identity;
            path.transform.localScale = new Vector3(0.02f, 0.02f, (end.position - start.position).magnitude);

            var arrow = Resources.Load<GameObject>("misc/gutter_arrowhead").inst();
            arrow.transform.SetParent(display.transform);
            arrow.transform.localPosition = Vector3.zero;
            arrow.transform.localRotation = Quaternion.identity;
            arrow.transform.localScale = Vector3.one * 0.1f;
        }

        display.SetActive(enabled);
    }
}
