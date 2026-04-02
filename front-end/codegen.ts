import type { CodegenConfig } from '@graphql-codegen/cli';

const GRAPHQL_ENDPOINT = process.env.VITE_PUBLIC_API_URL 
  ? `${process.env.VITE_PUBLIC_API_URL}/graphql`
  : 'http://localhost:5001/graphql';

const config: CodegenConfig = {
  schema: GRAPHQL_ENDPOINT,
  documents: [
    'src/**/*.graphql',
    'src/**/*.gql',
    'src/**/*.ts',
    'src/**/*.svelte',
    '!src/lib/services/graphql/generated/**'
  ],
  generates: {
    'src/lib/services/graphql/generated/': {
      preset: 'client',
      plugins: [],
      presetConfig: {
        gqlTagName: 'gql',
      },
      config: {
        useTypeImports: true,
      }
    },
    'src/lib/services/graphql/generated/types.ts': {
      plugins: [
        'typescript',
        'typescript-operations',
        'typescript-graphql-request',
      ],
      config: {
        useTypeImports: true,
        avoidOptionals: {
          field: true,
        },
        defaultScalarType: 'unknown',
        nonOptionalTypename: true,
        dedupeFragments: true,
        inlineFragmentTypes: 'combine',
        scalars: {
          DateTime: 'string',
          Date: 'string',
          JSON: 'Record<string, unknown>',
        },
      },
    },
  },
  ignoreNoDocuments: true,
};

export default config;
