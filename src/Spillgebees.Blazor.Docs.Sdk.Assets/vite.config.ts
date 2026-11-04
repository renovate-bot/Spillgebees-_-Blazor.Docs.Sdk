import { basename, resolve } from "node:path";
import { copyFileSync, mkdirSync, readdirSync } from "node:fs";
import { defineConfig, type UserConfig, type Plugin } from "vite";

const outDir = resolve(import.meta.dirname, "../Spillgebees.Blazor.Docs.Sdk/wwwroot");

/**
 * Fonts pulled from fontsource packages rather than vendored into src/fonts.
 * Only upright (non-italic) faces are shipped; the display and body families
 * are variable, so one file per unicode range covers every weight we use.
 */
const FONTSOURCE_FACES = [
  "@fontsource-variable/schibsted-grotesk/files/schibsted-grotesk-latin-wght-normal.woff2",
  "@fontsource-variable/schibsted-grotesk/files/schibsted-grotesk-latin-ext-wght-normal.woff2",
  "@fontsource-variable/hanken-grotesk/files/hanken-grotesk-latin-wght-normal.woff2",
  "@fontsource-variable/hanken-grotesk/files/hanken-grotesk-latin-ext-wght-normal.woff2",
];

/**
 * Copies font files into wwwroot/fonts/: the vendored ones from src/fonts/ and
 * the fontsource ones from node_modules. Nothing is fetched at runtime, so a
 * docs site makes no third-party font request.
 */
function copyFontsPlugin(): Plugin {
  return {
    name: "copy-fonts",
    writeBundle() {
      const fontsOut = resolve(outDir, "fonts");
      mkdirSync(fontsOut, { recursive: true });

      const fontsIn = resolve(import.meta.dirname, "src/fonts");
      for (const file of readdirSync(fontsIn)) {
        copyFileSync(resolve(fontsIn, file), resolve(fontsOut, file));
      }

      for (const spec of FONTSOURCE_FACES) {
        const from = resolve(import.meta.dirname, "node_modules", spec);
        copyFileSync(from, resolve(fontsOut, basename(spec)));
      }
    },
  };
}

/** Ships the custom-theme starting point alongside the compiled stylesheet. */
function copyThemeTemplatePlugin(): Plugin {
  return {
    name: "copy-theme-template",
    writeBundle() {
      copyFileSync(
        resolve(import.meta.dirname, "src/styles/docs-sdk-theme-template.css"),
        resolve(outDir, "docs-sdk-theme-template.css"),
      );
    },
  };
}

export default defineConfig(({ mode }) => {
  const isDev = mode === "development";

  return {
    build: {
      lib: {
        entry: resolve(import.meta.dirname, "src/index.ts"),
        formats: ["es"],
        fileName: () => "Spillgebees.Blazor.Docs.Sdk.lib.module.js",
      },
      outDir,
      emptyOutDir: false,
      sourcemap: isDev,
      minify: !isDev,
      target: "es2022",
      rollupOptions: {
        output: {
          assetFileNames: (assetInfo) => {
            if (assetInfo.names?.some((n) => n.endsWith(".css"))) {
              return "docs-sdk.css";
            }
            return assetInfo.names?.[0] ?? "[name][extname]";
          },
        },
      },
    },
    plugins: [copyFontsPlugin(), copyThemeTemplatePlugin()],
  } satisfies UserConfig;
});
