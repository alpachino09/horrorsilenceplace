using System;
using System.Collections.Generic;

namespace Silence.Core
{
    public static class CharacterIKPassRules
    {
        public static int FinalLayer(int upper,float upperWeight,int reaction,float reactionWeight)
            => Math.Max(upper>=0 && upperWeight>.001f?upper:0,reaction>=0 && reactionWeight>.001f?reaction:0);
        // A zero-weight callback after the chosen solve must not erase its goals.
        public static bool ClearBeforeSolve(int layer,int final)=>layer>=0 && layer<final;
    }
    public enum CharacterSurfaceRole { Unknown,Solid,Transparent }
    public static class CharacterSurfaceRules
    {
        private static CharacterSurfaceRole Name(string name)
        {
            string n=(name ?? "").ToLowerInvariant();
            foreach(string token in new[]{"glass","cornea","lens","hair_cards","eyelash","lashes"})if(n.Contains(token))return CharacterSurfaceRole.Transparent;
            foreach(string token in new[]{"body_clothed","face","skin","fabric","leather","footwear","sole","belt","teeth","tongue","mouth","metal","brows","hair_dark"})if(n.Contains(token))return CharacterSurfaceRole.Solid;
            return CharacterSurfaceRole.Unknown;
        }
        public static CharacterSurfaceRole Classify(string renderer,string material)
        {
            var role=Name(material);
            return role!=CharacterSurfaceRole.Unknown?role:Name(renderer);
        }
    }
    // The render owner is an opaque reference, so this stack can be tested
    // without graphics APIs. Frame state itself stays in the presentation layer.
    public sealed class CharacterRenderScopes<T>
    {
        private readonly struct Entry
        {
            public readonly object owner;public readonly T state;
            public Entry(object owner,T state){this.owner=owner;this.state=state;}
        }
        private readonly Stack<Entry> entries=new Stack<Entry>();
        public int Count=>entries.Count;
        public void Push(object owner,T state)
        {if(owner==null)throw new ArgumentNullException(nameof(owner));entries.Push(new Entry(owner,state));}
        public bool TryPopFor(object owner,out T state,out bool matched)
        {
            state=default;matched=false;if(owner==null)return false;bool found=false;
            foreach(var entry in entries)if(ReferenceEquals(entry.owner,owner)){found=true;break;}
            if(!found)return false;
            var top=entries.Pop();state=top.state;matched=ReferenceEquals(top.owner,owner);return true;
        }
        public void Clear()=>entries.Clear();
    }
}
