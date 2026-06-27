<template>
  <div>
    <div class="text-h6 font-weight-bold mb-6">Cadastros</div>
    <v-row>
      <v-col v-for="m in modulos" :key="m.to" cols="12" sm="6" md="4" lg="3">
        <v-card :to="m.to" rounded="xl" elevation="1" hover class="pa-6 text-center cursor-pointer">
          <v-icon :icon="m.icon" size="48" color="primary" class="mb-3" />
          <div class="text-subtitle-1 font-weight-bold mb-1">{{ m.titulo }}</div>
          <div class="text-caption text-medium-emphasis">{{ m.descricao }}</div>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
const modulos = [
  { icon: 'mdi-account-group-outline', titulo: 'Clientes',          descricao: 'Cadastro e histórico de compras',     to: '/cadastros/clientes' },
  { icon: 'mdi-truck-delivery-outline',titulo: 'Fornecedores',      descricao: 'Fornecedores e contatos',             to: '/cadastros/fornecedores' },
  { icon: 'mdi-tag-multiple-outline',  titulo: 'Categorias',        descricao: 'Categorias de produtos',              to: '/cadastros/categorias' },
  { icon: 'mdi-watermark',             titulo: 'Marcas',            descricao: 'Marcas de produtos',                  to: '/cadastros/marcas' },
  { icon: 'mdi-ruler-square',          titulo: 'Unidades de Medida',descricao: 'UN, KG, CX, ML…',                   to: '/cadastros/unidades-medida' },
  { icon: 'mdi-warehouse',             titulo: 'Locais de Estoque', descricao: 'Depósitos e prateleiras',             to: '/cadastros/locais-estoque' },
]
</script>
