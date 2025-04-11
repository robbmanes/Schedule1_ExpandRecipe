using ExpandRecipe;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using MelonLoader;
using System.Collections;
using System.Linq.Expressions;
using UnityEngine;
using UnityEngine.UI;
using static Il2CppRootMotion.FinalIK.RagdollUtility;
using static Il2CppScheduleOne.AvatarFramework.Equipping.AvatarEquippable;
using HarmonyPatch = HarmonyLib.HarmonyPatch;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(ExpandRecipe.Main), "ExpandRecipe", "0.2.0", "Robb Manes", "nexusmods")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ExpandRecipe
{
    public class Main : MelonMod
    {
        public string testedVersion = "0.3.3f15";

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg($"Tested on Schedule I version \"{testedVersion}\"");
        }

        public static bool DoesProductHaveRecipeWithItself(ProductDefinition product, out StationRecipe.IngredientQuantity baseIngredient) {
			foreach(StationRecipe recipe in product.Recipes) {
				foreach(StationRecipe.IngredientQuantity ingredient in recipe.Ingredients) {
                    // Does recipe have the product as an ingredient?
                    if(ingredient.Item.ID == recipe.Product.Item.ID) {
						baseIngredient = ingredient;
                        return true;
					}
				}
			}

            baseIngredient = null;
			return false;
		}

		public static List<ExpandedRecipe> GetExpandedRecipes(Il2CppSystem.Collections.Generic.List<StationRecipe> baseRecipes, ProductManager productManager) {
			List<ExpandedRecipe> expandedRecipes = [];

			foreach(StationRecipe baseRecipe in baseRecipes) {
				ExpandedRecipe baseExpandedRecipe = new ExpandedRecipe();
				baseExpandedRecipe.finalProduct = baseRecipe.Product;

				GetExpandedRecipes(baseRecipe, baseExpandedRecipe, ref expandedRecipes, productManager);
			}

            foreach(ExpandedRecipe expandedRecipe in expandedRecipes) {
				expandedRecipe.addedIngredients.Reverse();
			}

            return expandedRecipes;
		}

		public static void GetExpandedRecipes(StationRecipe baseRecipe, ExpandedRecipe expandedRecipe, ref List<ExpandedRecipe> expandedRecipes, ProductManager productManager)
        {
            // Sort ingredients so ingredients that are products get handled last
            List<StationRecipe.IngredientQuantity> sortedIngredients = baseRecipe.Ingredients.ToArray().OrderByDescending(i => i.Item.Category).ToList();

			for (int i= 0; i < sortedIngredients.Count; i++)
            {
                // Our ingredient is a product, we need to go deeper
                if(sortedIngredients[i].Item.Category == EItemCategory.Product)
                {
                    Func<ProductDefinition, bool> value = x => x.ID == sortedIngredients[i].Item.ID;
                    var product = productManager.AllProducts.Find(value) ?? throw new Exception($"Could not find base product for \"'{sortedIngredients[i].Item.Name}'\"");

                    if (product.Recipes.Count <= 0)
                    {
						// We're a product without a recipe, we must be the base
						expandedRecipe.baseProduct = sortedIngredients[i];

						// Base product is the last thing to be added
						if(i == sortedIngredients.Count - 1) {
							expandedRecipes.Add(expandedRecipe);
						}

						continue;
                    }

                    // We need to peek a layer down to see if the next recipe's ingredient is ourself
                    // This stops infinite recursion when we hit the bottom
                    // Example: Grandaddy Purple = Grandaddy Purple + Flu Medicine
                    if(DoesProductHaveRecipeWithItself(product, out StationRecipe.IngredientQuantity baseIngredient)) {
						expandedRecipe.baseProduct = baseIngredient;

						// Base product is the last thing to be added
						if(i == sortedIngredients.Count - 1) {
							expandedRecipes.Add(expandedRecipe);
						}

						continue;
					}

                    // We'll recurse a ton if we go through every possible recipe for every product in the line,
                    // so we're going to blindly trust the first recipe in subsequent products.
                    // This means we don't show every possible combination but it's better than
                    // taking a year whenever you click on a product to show all combos.
                    foreach(StationRecipe productRecipe in product.Recipes) {
                        GetExpandedRecipes(productRecipe, expandedRecipe.Copy(), ref expandedRecipes, productManager);
                    }
                }
                else
                {
                    expandedRecipe.addedIngredients.Add(sortedIngredients[i]);
				}
			}
		}

		public static GameObject CreateExpandedRecipesTextUIGO(GameObject gameObjectToClone, GameObject parentGameObject) {
			GameObject expandedRecipesTextUI;
			Transform expandedRecipesTextUITransform = parentGameObject.transform.Find("ExpandedRecipesText");
            if(expandedRecipesTextUITransform == null) {
				expandedRecipesTextUI = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
				expandedRecipesTextUI.name = "ExpandedRecipesText";
				expandedRecipesTextUI.GetComponent<Text>().text = "Expanded Recipe(s)";
			} else {
				expandedRecipesTextUI = expandedRecipesTextUITransform.gameObject;
			}

			expandedRecipesTextUI.gameObject.SetActive(true);

			return expandedRecipesTextUI;
		}

		public static GameObject GetOrCreateExpandedRecipesUIGO(GameObject parentGameObject) {
			GameObject expandedRecipesUI;
			Transform expandedRecipeUITransform = parentGameObject.transform.Find("ExpandedRecipes");
			if(expandedRecipeUITransform == null) {
                expandedRecipesUI = GameObject.Instantiate(new GameObject(), parentGameObject.transform).gameObject;
				expandedRecipesUI.name = "ExpandedRecipes";

				VerticalLayoutGroup verticalLayoutGroup = expandedRecipesUI.gameObject.AddComponent<VerticalLayoutGroup>();
                verticalLayoutGroup.spacing = 8;
				verticalLayoutGroup.childScaleHeight = false;
				verticalLayoutGroup.childScaleWidth = false;
				verticalLayoutGroup.childControlHeight = false;
				verticalLayoutGroup.childControlWidth = false;
				verticalLayoutGroup.childForceExpandHeight = false;
				verticalLayoutGroup.childForceExpandWidth = false;
			} else {
				expandedRecipesUI = expandedRecipeUITransform.gameObject;

				// Clear existing recipes
				expandedRecipesUI.DestroyChildren();
			}

			expandedRecipesUI.gameObject.SetActive(true);

			return expandedRecipesUI;
		}

        public static GameObject CreateExpandedRecipeUIGO(GameObject gameObjectToClone, GameObject parentGameObject) {
			GameObject expandedRecipeUI = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
			expandedRecipeUI.gameObject.SetActive(true);

			HorizontalLayoutGroup horizontalLayoutGroup = expandedRecipeUI.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
			horizontalLayoutGroup.childScaleHeight = false;
			horizontalLayoutGroup.childScaleWidth = false;
			horizontalLayoutGroup.childControlHeight = false;
			horizontalLayoutGroup.childControlWidth = false;
			horizontalLayoutGroup.childForceExpandHeight = false;
			horizontalLayoutGroup.childForceExpandWidth = true;

			// Clear cloned recipe
			expandedRecipeUI.DestroyChildren();

			return expandedRecipeUI;
		}

		public static GameObject CreateBaseProductUIGO(GameObject gameObjectToClone, GameObject parentGameObject, ExpandedRecipe expandedRecipe) {
			GameObject baseProductUI = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
			baseProductUI.GetComponent<Image>().sprite = expandedRecipe.baseProduct.Item.Icon;
			baseProductUI.GetComponent<Image>().preserveAspect = true;
			baseProductUI.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = expandedRecipe.baseProduct.Item.name;
            return baseProductUI;
		}

        public static GameObject CreatePlusUIGO(GameObject gameObjectToClone, GameObject parentGameObject) {
			GameObject plusClone = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
			plusClone.GetComponent<Image>().preserveAspect = true;
            return plusClone;
		}

        public static GameObject CreateMixUIGO(GameObject gameObjectToClone, GameObject parentGameObject, StationRecipe.IngredientQuantity ingredient) {
			GameObject mixClone = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
			mixClone.GetComponent<Image>().sprite = ingredient.Item.Icon;
			mixClone.GetComponent<Image>().preserveAspect = true;
			mixClone.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = ingredient.Item.name;
            return mixClone;
		}

        public static GameObject CreateArrowUIGO(GameObject gameObjectToClone, GameObject parentGameObject) {
			GameObject arrowClone = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
			arrowClone.GetComponent<Image>().preserveAspect = true;
            return arrowClone;
		}

        public static GameObject CreateOutputUIGO(GameObject gameObjectToClone, GameObject parentGameObject, ExpandedRecipe expandedRecipe) {
			GameObject outputClone = GameObject.Instantiate(gameObjectToClone, parentGameObject.transform).gameObject;
			outputClone.GetComponent<Image>().sprite = expandedRecipe.finalProduct.Item.Icon;
			outputClone.GetComponent<Image>().preserveAspect = true;
			outputClone.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = expandedRecipe.finalProduct.Item.name;
            return outputClone;
		}

        public static void BuildUIWithRecipe(List<ExpandedRecipe> expandedRecipes, GameObject recipeTextUI, GameObject recipesContainerUI)
        {
            // Use these to clone instead of doing it by hand
            GameObject recipeToCloneUI = recipesContainerUI.transform.Find("Recipe").gameObject ?? throw new Exception("Unable to find recipeUI GameObject");
            GameObject productToCloneUI = recipeToCloneUI.transform.Find("Product").gameObject ?? throw new Exception("Unable to find productUI GameObject");
            GameObject plusToCloneUI = recipeToCloneUI.transform.Find("Plus").gameObject ?? throw new Exception("Unable to find plusUI GameObject");
            GameObject mixerToCloneUI = recipeToCloneUI.transform.Find("Mixer").gameObject ?? throw new Exception("Unable to find mixerUI GameObject");
            GameObject arrowToCloneUI = recipeToCloneUI.transform.Find("Arrow").gameObject ?? throw new Exception("Unable to find arrowUI GameObject");
            GameObject outputToCloneUI = recipeToCloneUI.transform.Find("Output").gameObject ?? throw new Exception("Unable to find outputUI GameObject");

			CreateExpandedRecipesTextUIGO(recipeTextUI, recipesContainerUI);

			// The recipesContainer rect holds 20 default recipes, and we need to add/manage just our own
			GameObject expandedRecipesUI = GetOrCreateExpandedRecipesUIGO(recipesContainerUI);

			foreach(ExpandedRecipe expandedRecipe in expandedRecipes) {
				GameObject expandedRecipeUI = CreateExpandedRecipeUIGO(recipeToCloneUI, expandedRecipesUI);

				CreateBaseProductUIGO(productToCloneUI, expandedRecipeUI, expandedRecipe);

				int ingredientCount = expandedRecipe.addedIngredients.Count - 1;
				foreach(StationRecipe.IngredientQuantity ingredient in expandedRecipe.addedIngredients) {
					if(ingredientCount >= 0) {
						CreatePlusUIGO(plusToCloneUI, expandedRecipeUI);
					}

					CreateMixUIGO(mixerToCloneUI, expandedRecipeUI, ingredient);

					ingredientCount--;
				}

				CreateArrowUIGO(arrowToCloneUI, expandedRecipeUI);
				CreateOutputUIGO(outputToCloneUI, expandedRecipeUI, expandedRecipe);
			}
		}
    }

    // Patch the products selected in the ProductManagerApp
    [HarmonyPatch(typeof(ProductManagerApp), "SelectProduct")]
    public static class ProductManager_SelectProduct_Patch
    {
        public static void Prefix(ProductManagerApp __instance, ProductEntry entry)
        {
            ProductManager productManager;
			Il2CppSystem.Collections.Generic.List<StationRecipe> baseRecipes;
            Transform recipeText;
            Transform recipesContainer;
            List<ExpandedRecipe> expandedRecipes;

            // Make sure we can find the ProductManager
            try
            {
                productManager = Object.FindObjectsOfType<ProductManager>()[0];
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to find base Product Manager: {ex}");
                return;
            }

			// Set up the Phone UI
			// We have to do this early to clear existing ExpandedUI entries if they exist
			try
            {
                var detailPanel = __instance.DetailPanel;

				recipeText = detailPanel.transform.Find("Scroll View/Viewport/Content/Recipes");
				if(recipeText == null) {
					MelonLogger.Error("Can't find Recipes object in current scene");
					return;
				}

				recipesContainer = detailPanel.transform.Find("Scroll View/Viewport/Content/RecipesContainer");
                if (recipesContainer == null)
                {
                    MelonLogger.Error("Can't find RecipesContainer object in current scene");
                    return;
				}

				Transform existingExpandedRecipesText = recipesContainer.Find("ExpandedRecipesText");
				if(existingExpandedRecipesText != null) {
					// Disable existing Extended Recipe(s) text
					existingExpandedRecipesText.gameObject.SetActive(false);
				}

				Transform existingExpandedRecipes = recipesContainer.Find("ExpandedRecipes");
                if (existingExpandedRecipes != null)
                {
					// There's existing recipes - disable it
					existingExpandedRecipes.gameObject.SetActive(false);
				}
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to find Phone UI components: {ex}");
                return;
            }

            // Fill the base recipe or determine if we're the base product
            try
            {
                if (entry.Definition.Recipes.Count > 0)
                {
					baseRecipes = entry.Definition.Recipes;
                }
                else
                {
                    // We must be a base product, bail
                    return;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to find base definition: {ex}");
                return;
            }

            // Get the recipe, base product, and reverse the tree
            try {
				expandedRecipes = Main.GetExpandedRecipes(baseRecipes, productManager);

				// We must be a base product, bail
				if(expandedRecipes.Count == 0) {
					return;
				}

                // Recipe with no ingredients, bail
                if(expandedRecipes[0].addedIngredients.Count <= 0)
                {
                    return;
                }

			} catch (Exception ex) {
                MelonLogger.Error($"Exception raised getting recipe: {ex}");
                return;
            }

            // Actually update the UI
            try
            {
                Main.BuildUIWithRecipe(expandedRecipes, recipeText.gameObject, recipesContainer.gameObject);

            } catch (Exception ex) {
                MelonLogger.Error($"Exception raised building UI component: {ex}");
                return;
            }
#if DEBUG
            // Might as well print it to the Melon console
            string recipeString = $"{baseProduct.Item.Name}";
            foreach (StationRecipe.IngredientQuantity ingredient in expandedRecipe)
            {
                recipeString += " + ";
                recipeString += $"{ingredient.Item.name}";
            }
            MelonLogger.Msg($"Expanded Recipe for \"{baseRecipe.Product.Item.Name}\": {recipeString}");
#endif
        }
    }
}