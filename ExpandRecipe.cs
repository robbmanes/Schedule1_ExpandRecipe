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
using Il2CppSteamworks;
using Il2CppScheduleOne.UI;

[assembly: MelonInfo(typeof(ExpandRecipe.Main), "ExpandRecipe", "0.1.3", "Robb Manes", "nexusmods")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ExpandRecipe
{
    public class Main : MelonMod
    {
        public string testedVersion = "0.3.4f4";

        private static readonly List<string> bottomProducts = [
            "Granddaddy Purple",
            "OG Kush",
            "Green Crack",
            "Sour Diesel",
            "Meth",
            "Cocaine"
        ];

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg($"Tested on Schedule I version \"{testedVersion}\"");
        }

        public static List<StationRecipe.IngredientQuantity> GetExpandedRecipe(StationRecipe baseRecipe, ref ProductManager productManager, ref StationRecipe.IngredientQuantity baseProduct)
        {
            List<StationRecipe.IngredientQuantity> mixerSortedList = [];
            List<StationRecipe.IngredientQuantity> expandedRecipe = [];

            // Order our list to be base mixers first, always
            // Not doing this causes out-of-order problems
            // No List.OrderBy in IL2Cpp?
            foreach (StationRecipe.IngredientQuantity sortIngredient in baseRecipe.Ingredients)
            {
                if (sortIngredient.Item.Category == EItemCategory.Product)
                {
                    mixerSortedList = mixerSortedList.Append(sortIngredient).ToList();
                }
                else
                {
                    mixerSortedList = mixerSortedList.Prepend(sortIngredient).ToList();
                }
            }

            MelonLogger.Msg($"Recipe for \"{baseRecipe.Product.Item.name}\"");
            foreach (StationRecipe.IngredientQuantity ingredient in mixerSortedList)
            {
                MelonLogger.Msg($"\t{ingredient.Item.name}\"");
            }

            foreach (StationRecipe.IngredientQuantity ingredient in mixerSortedList)
            {
                // If one of our ingredients is a bottom-level product, make it the base and quit this cycle
                if (bottomProducts.Contains(ingredient.Item.name))
                {
                    baseProduct = ingredient;
                    continue;
                }

                // Our ingredient is a product that isn't a bottomProduct, we need to go deeper
                if (ingredient.Item.Category == EItemCategory.Product)
                {
                    Func<ProductDefinition, bool> value = x => x.ID == ingredient.Item.ID;
                    var product = productManager.AllProducts.Find(value) ?? throw new Exception($"Could not find base product for \"'{ingredient.Item.Name}'\"");

                    // We hit a base product with no further recipes
                    // This shouldn't happen if bottomProducts is up to date, but since it's possible we should check for it and log it appropriately
                    if (product.Recipes.Count <= 0)
                    {
                        MelonLogger.Error($"Hit unknown base product \"{product.name}\" not in bottomProducts list, mod may need updating");
                        baseProduct = ingredient;
                        continue;
                    }

                    // Recursion safety check, make sure we're not in our product's own recipes (1 level down)
                    foreach (StationRecipe nextRecipe in product.Recipes)
                    {
                        foreach (StationRecipe.IngredientQuantity nextIngredient in nextRecipe.Ingredients)
                        {
                            if (nextIngredient.Item.name == product.name) continue;
                        }
                    }

                    // We never want to return a list with a product in it, only ingredients, so recursively call ourselves on the new product
                    // We blindly take the first recipe (for now) for performance/branch control reasons
                    MelonLogger.Msg($"Deriving recipe for next product \"{product.name}\"");
                    var recursiveList = GetExpandedRecipe(product.Recipes[0], ref productManager, ref baseProduct);

                    // Append the entire recursive list if not empty
                    if (recursiveList.Count > 0)
                    {
                        expandedRecipe.AddRange(recursiveList);
                    }
                }
                // Add just the single ingredient to the list
                else
                {
                    expandedRecipe.Add(ingredient);
                    MelonLogger.Msg($"Added regular mixer \"{ingredient.Item.name}\"");
                }
            }
            return expandedRecipe;
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
            List<StationRecipe.IngredientQuantity> expandedRecipe;
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

            // Get the recipe and base product
            try
            {
                expandedRecipe = Main.GetExpandedRecipe(baseRecipe, ref productManager, ref baseProduct);
                expandedRecipe.Reverse();

                // Recipe with no ingredients, bail
                if (expandedRecipe.Count <= 0)
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
                Main.BuildUIWithRecipe(baseRecipe.Product, expandedRecipe, baseProduct, recipesContainer.gameObject);

            } catch (Exception ex) {
                MelonLogger.Error($"Exception raised building UI component: {ex}");
                return;
            }

            // Might as well print it to the Melon console
            string recipeString = $"{baseProduct.Item.Name}";
            foreach (StationRecipe.IngredientQuantity ingredient in expandedRecipe)
            {
                recipeString += " + ";
                recipeString += $"{ingredient.Item.name}";
            }
            MelonLogger.Msg($"Expanded Recipe for \"{baseRecipe.Product.Item.Name}\": {recipeString}");
        }
    }
}