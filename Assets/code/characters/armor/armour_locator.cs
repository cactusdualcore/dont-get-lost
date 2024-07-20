﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Used to locate a piece of armour on an object. </summary>
public class armour_locator : MonoBehaviour
{
    /// <summary> The size of the bounding box that armour pieces will be stretched to fill. </summary>
    public Vector3 size = Vector3.one;

    /// <summary> The location on the body that this locator corresponds to. </summary>
    public armour_piece.LOCATION location;

    /// <summary> The side of the body that this locator corresponds to. </summary>
    public armour_piece.HANDEDNESS handedness = armour_piece.HANDEDNESS.EITHER;

    /// <summary> The pice of armour actually equipped in this location. This should
    /// be set to a prefab, which will be instanced, or null. The previously
    /// equipped piece of armour (if it exists) will be destroyed. </summary>
    public armour_piece equipped
    {
        get => _equipped;
        set
        {
            if (_equipped == value)
                return; // No change

            // Destroy the previously-equipped piece
            if (_equipped != null)
                Destroy(_equipped.gameObject);

            _equipped = value;

            if (_equipped == null)
                return;

            // Mirror left-handed pieces
            float x_mod = handedness == armour_piece.HANDEDNESS.LEFT ? -1f : 1f;

            // Create the newly-equipped peice
            _equipped = _equipped.inst();
            _equipped.transform.SetParent(null);
            _equipped.transform.localScale = new Vector3(
                x_mod * size.x / _equipped.size.x,
                size.y / _equipped.size.y,
                size.z / _equipped.size.z
            );

            // Scale armour
            _equipped.transform.localScale *= GetComponentInParent<ICanEquipArmour>().armour_scale();

            _equipped.transform.SetParent(transform);
            _equipped.transform.localPosition = Vector3.zero;
            _equipped.transform.localRotation = Quaternion.identity;
        }
    }
    armour_piece _equipped;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }

#if UNITY_EDITOR
    [UnityEditor.CanEditMultipleObjects()]
    [UnityEditor.CustomEditor(typeof(armour_locator))]
    public class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var al = (armour_locator)target;
            al.equipped = (armour_piece)UnityEditor.EditorGUILayout.ObjectField
                ("Equipped", al.equipped, typeof(armour_piece), true);
        }
    }
#endif
}