using System;
using UnityEngine;

namespace UmdJam.Multiplayer
{
    [CreateAssetMenu(menuName = "Umd Jam/Character Skin Catalog", fileName = "CharacterSkinCatalog")]
    public sealed class CharacterSkinCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string displayName = "Character";
            [SerializeField] private GameObject prefab;
            [SerializeField] private Avatar avatar;
            [SerializeField] private Material materialOverride;
            [SerializeField] private Vector3 localPosition;
            [SerializeField] private Vector3 localEulerAngles;
            [SerializeField] private Vector3 localScale = Vector3.one;

            public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Character" : displayName;
            public GameObject Prefab => prefab;
            public Avatar Avatar => avatar;
            public Material MaterialOverride => materialOverride;
            public Vector3 LocalPosition => localPosition;
            public Vector3 LocalEulerAngles => localEulerAngles;
            public Vector3 LocalScale => localScale;
        }

        [SerializeField] private RuntimeAnimatorController animationController;
        [SerializeField] private Entry[] skins = Array.Empty<Entry>();

        public RuntimeAnimatorController AnimationController => animationController;
        public int Count => skins?.Length ?? 0;

        public Entry Get(int index)
        {
            return index >= 0 && index < Count ? skins[index] : null;
        }
    }
}
