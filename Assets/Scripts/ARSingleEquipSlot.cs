using UnityEngine;

public static class ARSingleEquipSlot
{
    private static GameObject equippedObject;

    public static GameObject EquippedObject => equippedObject;
    public static bool HasEquippedObject => equippedObject != null;

    public static bool CanEquip(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        return equippedObject == null || equippedObject == candidate;
    }

    public static bool TryEquip(GameObject candidate)
    {
        if (!CanEquip(candidate))
        {
            return false;
        }

        equippedObject = candidate;
        return true;
    }

    public static void Release(GameObject holder)
    {
        if (equippedObject == holder)
        {
            equippedObject = null;
        }
    }

    public static void ResetSlot()
    {
        equippedObject = null;
    }
}
