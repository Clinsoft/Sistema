using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Seed da base nutricional TACO (UNICAMP) e TBCA (USP) — valores por 100g ou 100ml.
    /// </summary>
    public partial class Seed_AlimentosTaco : Migration
    {
        protected override void Up(MigrationBuilder m)
        {
            void ins(string nome, string grupo, string fonte,
                double? kcal, double? carb, double? prot, double? lip,
                double? fibra, double? sodio, double? calcio, double? ferro,
                double? gordSat, double? gordTrans, double? vitC, double? vitA,
                double? zinc, double? potassio)
            {
                m.Sql($@"
IF NOT EXISTS (SELECT 1 FROM AlimentosTaco WHERE Nome = N'{nome}')
INSERT INTO AlimentosTaco (Id, Nome, GrupoAlimentar, Fonte, CaloriasKcal,
  Carboidratos, Proteinas, LipidiosTotais, FibraAlimentar, Sodio,
  Calcio, Ferro, GordurasSaturadas, GordurasTrans, VitaminaC, VitaminaA,
  Zinco, Potassio, CriadoEm)
VALUES (NEWID(), N'{nome}', N'{grupo}', N'{fonte}',
  {F(kcal)},{F(carb)},{F(prot)},{F(lip)},{F(fibra)},{F(sodio)},
  {F(calcio)},{F(ferro)},{F(gordSat)},{F(gordTrans)},{F(vitC)},{F(vitA)},
  {F(zinc)},{F(potassio)}, GETUTCDATE())");
            }

            // Cereais e derivados
            ins("Arroz integral cozido","Cereais e derivados","TACO",124,25.8,2.6,1.0,2.7,2,5,0.3,0.2,0,0,0,0.8,26);
            ins("Arroz branco cozido","Cereais e derivados","TACO",128,28.1,2.5,0.2,1.6,1,4,0.1,0.1,0,0,0,0.4,24);
            ins("Aveia em flocos","Cereais e derivados","TACO",394,66.6,13.9,8.5,9.1,4,54,4.7,1.5,0,0,0,3.1,355);
            ins("Quinoa cozida","Cereais e derivados","TBCA",120,21.3,4.4,1.9,2.8,7,17,1.5,0.2,0,0,0,1.1,172);
            ins("Pão integral","Cereais e derivados","TACO",253,43.9,8.6,4.0,6.9,427,91,2.5,0.8,0.1,0,0,1.1,228);
            ins("Granola sem açúcar","Cereais e derivados","TBCA",380,55.0,10.0,14.0,7.0,10,40,3.5,2.0,0,0,0,2.0,280);
            ins("Farinha de aveia","Cereais e derivados","TACO",394,66.6,13.9,8.5,9.1,4,54,4.7,1.5,0,0,0,3.1,355);
            ins("Farinha de trigo integral","Cereais e derivados","TACO",331,62.2,13.2,2.7,9.7,3,34,4.6,0.5,0,0,0,2.6,370);
            ins("Macarrão integral cozido","Cereais e derivados","TACO",133,26.3,4.5,0.8,3.2,1,11,0.8,0.2,0,0,0,0.9,72);

            // Leguminosas
            ins("Feijão-carioca cozido","Leguminosas","TACO",76,13.6,4.8,0.5,8.4,2,27,1.3,0.1,0,0.8,0,0.5,255);
            ins("Feijão-preto cozido","Leguminosas","TACO",77,14.0,4.5,0.5,8.4,2,29,1.5,0.1,0,0.8,0,0.6,255);
            ins("Grão-de-bico cozido","Leguminosas","TACO",164,27.4,8.9,2.6,7.6,7,49,2.9,0.3,0,0,1.5,1.5,291);
            ins("Lentilha cozida","Leguminosas","TACO",93,15.0,7.0,0.4,7.9,2,19,3.3,0.1,0,1.5,0,1.0,369);
            ins("Soja em grão cozida","Leguminosas","TACO",141,10.3,14.8,6.4,7.0,1,102,3.0,0.9,0,1.7,0,1.0,485);
            ins("Ervilha cozida","Leguminosas","TACO",75,12.4,5.2,0.5,5.3,2,27,1.2,0.1,0,14.0,0.4,1.0,168);
            ins("Proteína de soja texturizada seca","Leguminosas","TBCA",331,36.4,51.5,1.3,14.8,10,250,10.0,0.2,0,0,0,3.0,1900);

            // Carnes, ovos e peixes
            ins("Frango peito grelhado sem pele","Carnes e derivados","TACO",163,0,32.9,3.3,0,69,9,0.6,1.0,0,0,0,0.9,313);
            ins("Carne bovina patinho moído cozido","Carnes e derivados","TACO",215,0,28.8,10.6,0,70,8,3.6,4.0,0.3,0,0,5.8,350);
            ins("Ovo de galinha inteiro cozido","Ovos e derivados","TACO",146,1.1,13.3,9.5,0,148,50,1.8,3.0,0,0,95,1.4,138);
            ins("Clara de ovo cozida","Ovos e derivados","TACO",52,1.1,10.9,0.2,0,166,8,0.1,0,0,0,0,0.03,144);
            ins("Atum em conserva escorrido","Peixes e frutos do mar","TACO",163,0,28.9,5.1,0,333,15,1.3,1.4,0,0,0,0.9,280);
            ins("Tilápia filé grelhado","Peixes e frutos do mar","TACO",129,0,26.2,2.8,0,55,15,0.4,0.8,0,0,0,0.6,378);

            // Laticínios e suplementos
            ins("Leite integral pasteurizado","Leite e derivados","TACO",61,4.8,3.0,3.3,0,41,110,0.04,2.0,0.1,0.9,3,0.4,152);
            ins("Leite desnatado","Leite e derivados","TACO",35,5.1,3.3,0.1,0,50,123,0.04,0.1,0,0.9,3,0.4,163);
            ins("Iogurte natural integral","Leite e derivados","TACO",61,4.9,3.5,3.3,0,46,121,0.05,2.1,0.1,0.7,1,0.6,170);
            ins("Queijo minas frescal","Leite e derivados","TACO",264,3.2,17.4,20.9,0,388,579,0.4,13.4,0.8,0,112,1.1,104);
            ins("Ricota fresca","Leite e derivados","TACO",174,3.7,11.8,13.0,0,84,211,0.3,8.5,0.5,0,66,1.1,118);
            ins("Whey Protein concentrado 80%","Suplementos","TBCA",385,8.0,78.0,7.0,0,200,150,0.5,4.0,0,0,0,2.0,400);

            // Frutas
            ins("Banana nanica","Frutas","TACO",92,23.8,1.3,0.1,2.0,2,8,0.3,0,0,21.6,4,0.2,376);
            ins("Maçã com casca","Frutas","TACO",56,15.2,0.3,0.2,1.3,2,4,0.1,0,0,5.7,3,0.04,107);
            ins("Laranja pêra","Frutas","TACO",37,8.9,1.0,0.1,1.0,1,22,0.1,0,0,53.3,7,0.07,177);
            ins("Morango","Frutas","TACO",30,6.5,0.8,0.3,1.8,2,14,0.4,0,0,58.8,2,0.14,166);
            ins("Mamão papaia","Frutas","TACO",40,10.4,0.5,0.1,1.8,8,20,0.1,0,0,61.8,47,0.08,257);
            ins("Abacate","Frutas","TACO",96,6.0,1.2,8.4,6.3,4,12,0.5,1.3,0,17.4,3,0.64,485);
            ins("Açaí polpa","Frutas","TACO",58,6.0,1.2,5.0,0,28,21,0.5,1.7,0,9.0,0,0.3,105);
            ins("Limão tahiti","Frutas","TACO",32,10.5,0.9,0.3,2.4,3,8,0.3,0,0,38.2,2,0.06,130);

            // Hortaliças
            ins("Espinafre cru","Hortaliças","TACO",22,3.4,2.9,0.4,2.2,79,130,3.6,0.1,0,28.1,469,0.5,558);
            ins("Couve-folha crua","Hortaliças","TACO",32,5.4,3.8,0.6,2.0,30,250,1.0,0.1,0,120.0,769,0.4,450);
            ins("Brócolis cozido","Hortaliças","TACO",35,6.4,3.6,0.5,3.0,27,94,1.1,0.1,0,44.3,77,0.4,293);
            ins("Cenoura crua","Hortaliças","TACO",35,8.1,0.9,0.2,3.2,75,29,0.3,0,0,5.3,833,0.2,320);
            ins("Beterraba cozida","Hortaliças","TACO",43,9.8,1.5,0.1,2.8,65,16,0.7,0,0,4.9,2,0.3,335);
            ins("Tomate cru","Hortaliças","TACO",15,3.1,1.1,0.2,1.2,4,11,0.3,0,0,21.2,45,0.17,222);
            ins("Batata inglesa cozida","Hortaliças","TACO",52,12.0,1.2,0.1,1.9,4,5,0.3,0,0,7.5,0,0.3,380);
            ins("Mandioca crua","Hortaliças","TACO",151,35.5,1.2,0.3,1.9,9,30,0.5,0.1,0,20.6,1,0.3,271);

            // Sementes e oleaginosas
            ins("Chia semente","Sementes e oleaginosas","TBCA",486,42.1,16.5,30.7,34.4,16,631,7.7,3.3,0,1.6,0,4.6,407);
            ins("Linhaça dourada","Sementes e oleaginosas","TACO",495,28.9,18.3,42.2,27.3,6,199,5.7,4.3,0,0.6,0,4.3,813);
            ins("Castanha-do-pará","Sementes e oleaginosas","TACO",643,15.1,14.3,63.5,7.9,3,160,2.4,15.1,0,0.7,0,4.1,659);
            ins("Castanha de caju torrada sem sal","Sementes e oleaginosas","TACO",570,29.1,18.5,46.4,3.0,12,37,6.7,9.2,0,0,0,5.6,565);
            ins("Amendoim torrado sem sal","Sementes e oleaginosas","TACO",581,19.7,26.2,49.2,8.5,3,53,2.3,7.7,0,0,0,3.3,658);
            ins("Amêndoa","Sementes e oleaginosas","TBCA",579,21.6,21.2,49.9,12.5,1,264,3.7,3.8,0,0,0,3.1,733);
            ins("Nozes","Sementes e oleaginosas","TACO",620,18.4,15.2,59.4,6.7,2,61,2.3,5.6,0,1.3,0,3.1,441);
            ins("Semente de girassol","Sementes e oleaginosas","TBCA",584,20.0,20.8,51.5,8.6,3,78,5.3,5.4,0,1.4,0,5.0,645);
            ins("Semente de abóbora","Sementes e oleaginosas","TBCA",559,10.7,30.2,49.1,6.0,7,46,8.8,9.5,0,1.9,0,7.6,809);
            ins("Gergelim","Sementes e oleaginosas","TACO",565,26.0,18.1,47.9,8.0,3,975,14.6,6.7,0,0,0,7.8,468);

            // Óleos, gorduras e mel
            ins("Azeite de oliva extravirgem","Óleos e gorduras","TACO",884,0,0,100.0,0,2,1,0.4,14.4,0,0,0,0,1);
            ins("Óleo de coco extravirgem","Óleos e gorduras","TBCA",862,0,0,100.0,0,0,0,0.1,86.5,0,0,0,0,0);
            ins("Mel de abelha","Açúcares e doces","TACO",309,83.7,0.3,0,0,6,6,0.4,0,0,0.5,0,0.2,52);
            ins("Açúcar mascavo","Açúcares e doces","TACO",375,97.0,0.1,0,0,2,83,1.1,0,0,0,0,0.1,160);
            ins("Açúcar demerara","Açúcares e doces","TBCA",390,97.5,0,0,0,3,30,0.3,0,0,0,0,0.04,100);
            ins("Melado de cana","Açúcares e doces","TACO",276,68.8,0.5,0,0,30,228,4.6,0,0,0,0,0.6,1500);

            // Especiarias e derivados do cacau
            ins("Cúrcuma em pó","Ervas e condimentos","TBCA",354,64.9,7.8,9.9,21.1,38,168,55.0,3.1,0,25.9,0,4.4,2080);
            ins("Gengibre fresco","Ervas e condimentos","TACO",57,11.5,1.8,0.8,2.0,13,16,0.6,0.2,0,5.0,0,0.3,415);
            ins("Canela em pó","Ervas e condimentos","TBCA",247,80.6,3.9,1.2,53.1,10,1002,8.3,0.3,0,3.8,15,1.8,431);
            ins("Cacau em pó 100%","Cacau e derivados","TBCA",228,57.9,19.6,13.7,33.2,21,128,13.9,8.1,0,0,0,6.8,1524);
            ins("Coco ralado sem açúcar","Frutas","TACO",354,34.8,3.4,34.7,8.5,15,26,3.3,30.8,0,1.5,0,1.1,543);
            ins("Spirulina em pó","Algas e superalimentos","TBCA",290,23.9,57.5,7.7,3.6,1048,120,28.5,2.6,0,0,0,3.7,1363);
            ins("Clorela em pó","Algas e superalimentos","TBCA",410,23.2,58.0,9.3,0.3,85,221,130.0,2.1,0,0,0,7.1,1480);
            ins("Maca peruana em pó","Raízes e tubérculos","TBCA",325,70.7,14.0,2.2,7.1,19,182,14.7,0.6,0,0,0,3.8,2400);

            // Bebidas e extratos
            ins("Leite de coco","Bebidas e extratos","TACO",200,4.4,2.1,20.6,0,15,18,0.5,18.3,0,0,0,0.6,263);
            ins("Leite de soja sem açúcar","Bebidas e extratos","TACO",40,3.1,3.3,1.8,0,51,25,0.5,0.3,0,0,0,0.4,141);
            ins("Leite de amêndoas sem açúcar","Bebidas e extratos","TBCA",17,0.3,0.6,1.5,0.2,58,120,0.1,0.1,0,0,0,0.1,60);
            ins("Suco de laranja natural","Sucos e bebidas","TACO",42,9.9,0.7,0.2,0.4,1,11,0.1,0,0,50.0,6,0.05,200);
        }

        protected override void Down(MigrationBuilder m)
        {
            m.Sql("DELETE FROM AlimentosTaco");
        }

        private static string F(double? v) =>
            v.HasValue ? v.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "NULL";
    }
}
