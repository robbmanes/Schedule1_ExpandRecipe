using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ExpandRecipe {
	public static class GameObjectExtensions {
		public static void DestroyChildren(this GameObject thisGO) {
			int childCount = thisGO.transform.GetChildCount();
			for(int i = childCount - 1; i >= 0; i--) {
				var child = thisGO.transform.GetChild(i);
				GameObject.Destroy(child.gameObject);
			}
		}
	}
}
