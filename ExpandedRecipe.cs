using Il2CppScheduleOne.StationFramework;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpandRecipe {
	public class ExpandedRecipe {
		public StationRecipe.IngredientQuantity baseProduct;
		public List<StationRecipe.IngredientQuantity> addedIngredients;
		public StationRecipe.ItemQuantity finalProduct;

		public ExpandedRecipe() {
			addedIngredients = new List<StationRecipe.IngredientQuantity> { };
		}

		public ExpandedRecipe Copy() {
			ExpandedRecipe copy = new ExpandedRecipe();
			copy.baseProduct = baseProduct;
			copy.addedIngredients = [.. addedIngredients];
			copy.finalProduct = finalProduct;
			return copy;
		}

		public override string ToString() {
			return (baseProduct?.Item.Name ?? "null") + " + " + string.Join(" + ", addedIngredients.Select(i => i.Item.Name)) + " -> " + finalProduct.Item.Name;
		}
	}
}
