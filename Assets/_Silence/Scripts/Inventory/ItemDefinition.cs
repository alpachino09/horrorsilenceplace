using UnityEngine;

namespace Silence.Inventory
{
    [CreateAssetMenu(menuName = "SILENCE/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string id = "battery";
        public string labelRu = "Батарейка";
        public string labelEn = "Battery";
    }
}
