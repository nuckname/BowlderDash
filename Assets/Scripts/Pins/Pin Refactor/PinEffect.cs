using System;
using UnityEngine;

// Anything that is inherited from pin effect references this git link.
// https://gist.github.com/adammyhre/8e6f57d24e96d33738273ed0570828be

[Serializable]
public abstract class PinEffect 
{
    public abstract void Execute(PinBase context, Collider collider = null);
    
    public virtual void ResetEffect() { }
}