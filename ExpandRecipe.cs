using System.Collections;
using ExpandRecipe;
using MelonLoader;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.ItemFramework;
using Object = UnityEngine.Object;
using HarmonyPatch = HarmonyLib.HarmonyPatch;
using UnityEngine;
using UnityEngine.UI;
using System.Linq.Expressions;

[assembly: MelonInfo(typeof(ExpandRecipe.Main), "ExpandRecipe", "0.1.1", "Robb Manes", "nexusmods")]
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

        public static void GetExpandedRecipe(StationRecipe baseRecipe, ref List<StationRecipe.IngredientQuantity> expandedRecipe, ProductManager productManager, ref StationRecipe.IngredientQuantity baseProduct)
        {
            foreach (StationRecipe.IngredientQuantity ingredient in baseRecipe.Ingredients)
            {
                // Our ingredient is a product, we need to go deeper
                if (ingredient.Item.Category == EItemCategory.Product)
                {
                    Func<ProductDefinition, bool> value = x => x.ID == ingredient.Item.ID;
                    var product = productManager.AllProducts.Find(value) ?? throw new Exception($"Could not find base product for \"'{ingredient.Item.Name}'\"");

                    if (product.Recipes.Count <= 0)
                    {
                        // We're a product without a recipe, we must be the base
                        baseProduct = ingredient;
                        continue;
                    }

                    // We need to peek a layer down to see if the next recipe's ingredient is ourself
                    // This stops infinite recursion when we hit the bottom
                    // Example: Grandaddy Purple = Grandaddy Purple + Flu Medicine
                    foreach (StationRecipe nextRecipe in product.Recipes)
                    {
                        foreach (StationRecipe.IngredientQuantity i in nextRecipe.Ingredients)
                        {
                            // Did we hit bottom?
                            if (i.Item.ID == ingredient.Item.ID)
                            {
                                // Yep, add the base ingredient/product and bail
                                baseProduct = i;
                                return;
                            }
                        }
                    }
                    // We'll recurse a ton if we go through every possible recipe for every product in the line,
                    // so we're going to blindly trust the first recipe in subsequent products.
                    // This means we don't show every possible combination but it's better than
                    // taking a year whenever you click on a product to show all combos.
                    GetExpandedRecipe(product.Recipes[0], ref expandedRecipe, productManager, ref baseProduct);
                }
                else
                {
                    expandedRecipe.Add(ingredient);
                }
            }
        }

        public static void BuildUIWithRecipe(StationRecipe.ItemQuantity finalProduct, List<StationRecipe.IngredientQuantity> expandedRecipe, StationRecipe.IngredientQuantity baseProduct, GameObject recipesContainerUI)
        {
            // Use these to clone instead of doing it by hand
            GameObject recipeToCloneUI = recipesContainerUI.transform.Find("Recipe").gameObject ?? throw new Exception("Unable to find recipeUI GameObject");
            GameObject productToCloneUI = recipeToCloneUI.transform.Find("Product").gameObject ?? throw new Exception("Unable to find productUI GameObject");
            GameObject plusToCloneUI = recipeToCloneUI.transform.Find("Plus").gameObject ?? throw new Exception("Unable to find plusUI GameObject");
            GameObject mixerToCloneUI = recipeToCloneUI.transform.Find("Mixer").gameObject ?? throw new Exception("Unable to find mixerUI GameObject");
            GameObject arrowToCloneUI = recipeToCloneUI.transform.Find("Arrow").gameObject ?? throw new Exception("Unable to find arrowUI GameObject");
            GameObject outputToCloneUI = recipeToCloneUI.transform.Find("Output").gameObject ?? throw new Exception("Unable to find outputUI GameObject");

            // The recipesContainer rect holds 20 default recipes, and we need to add/manage just our own
            GameObject expandedRecipeUI;
            Transform expandedRecipeUITransform = recipesContainerUI.transform.Find("ExpandedRecipe");
            if (expandedRecipeUITransform == null)
            {
                expandedRecipeUI = Object.Instantiate(recipeToCloneUI, recipesContainerUI.transform).gameObject ?? throw new Exception("Failed to instantiate new ExpandedRecipeUI");
                expandedRecipeUI.name = "ExpandedRecipe";
                expandedRecipeUI.gameObject.SetActive(true);
                expandedRecipeUI.gameObject.AddComponent<HorizontalLayoutGroup>();
                expandedRecipeUI.transform.GetComponent<HorizontalLayoutGroup>().childScaleWidth = false;
            }
            else
            {
                expandedRecipeUI = expandedRecipeUITransform.gameObject;
                expandedRecipeUI.gameObject.SetActive(true);
            }

            // Always clear all children before using the recipe object
            int childCount = expandedRecipeUI.transform.GetChildCount();
            for (int i = childCount - 1; i >= 0; i--)
            {
                var child = expandedRecipeUI.transform.GetChild(i);
                GameObject.Destroy(child.gameObject);
            }

            GameObject baseProductUI = GameObject.Instantiate(productToCloneUI, expandedRecipeUI.transform).gameObject;
            baseProductUI.GetComponent<Image>().sprite = baseProduct.Item.Icon;
            baseProductUI.GetComponent<Image>().preserveAspect = true;
            baseProductUI.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = baseProduct.Item.name;

            int ingredientCount = expandedRecipe.Count - 1;
            foreach (StationRecipe.IngredientQuantity ingredient in expandedRecipe)
            {
                if (ingredientCount >= 0)
                {
                    GameObject plusClone = GameObject.Instantiate(plusToCloneUI, expandedRecipeUI.transform).gameObject;
                    plusClone.GetComponent<Image>().preserveAspect = true;
                }

                GameObject mixClone = GameObject.Instantiate(mixerToCloneUI, expandedRecipeUI.transform).gameObject;
                mixClone.GetComponent<Image>().sprite = ingredient.Item.Icon;
                mixClone.GetComponent<Image>().preserveAspect = true;
                mixClone.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = ingredient.Item.name;

                ingredientCount--;
            }

            GameObject arrowClone = GameObject.Instantiate(arrowToCloneUI, expandedRecipeUI.transform).gameObject;
            arrowClone.GetComponent<Image>().preserveAspect = true;

            GameObject outputClone = GameObject.Instantiate(outputToCloneUI, expandedRecipeUI.transform).gameObject;
            outputClone.GetComponent<Image>().sprite = finalProduct.Item.Icon;
            outputClone.GetComponent<Image>().preserveAspect = true;
            outputClone.GetComponent<Il2CppScheduleOne.UI.Tooltips.Tooltip>().text = finalProduct.Item.name;
        }
    }

    // Patch the products selected in the ProductManagerApp
    [HarmonyPatch(typeof(ProductManagerApp), "SelectProduct")]
    public static class ProductManager_SelectProduct_Patch
    {
        public static void Prefix(ProductManagerApp __instance, ProductEntry entry)
        {
            ProductManager productManager;
            StationRecipe baseRecipe;
            Transform recipesContainer;
            List<StationRecipe.IngredientQuantity> expandedRecipe = new List<StationRecipe.IngredientQuantity> { };
            StationRecipe.IngredientQuantity baseProduct = new StationRecipe.IngredientQuantity();

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
                recipesContainer = detailPanel.transform.Find("Scroll View/Viewport/Content/RecipesContainer");
                if (recipesContainer == null)
                {
                    MelonLogger.Error("Can't find RecipesContainer object in current scene");
                    return;
                }

                Transform existingExpandedRecipeEntry = recipesContainer.Find("ExpandedRecipe");
                if (existingExpandedRecipeEntry != null)
                {
                    // There's an existing entry - disable it
                    existingExpandedRecipeEntry.gameObject.SetActive(false);
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
                    baseRecipe = entry.Definition.Recipes[0];
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
            try
            {
                Main.GetExpandedRecipe(baseRecipe, ref expandedRecipe, productManager, ref baseProduct);

                // Recipe with no ingredients, bail
                if (expandedRecipe.Count <= 0)
                {
                    return;
                }
                expandedRecipe.Reverse();
            } catch (Exception ex) {
                MelonLogger.Error($"Exception raised getting recipe: {ex}");
                return;
            }

            // Actually update the UI
            try
            {
                Main.BuildUIWithRecipe(baseRecipe.Product, expandedRecipe, baseProduct, recipesContainer.gameObject);

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