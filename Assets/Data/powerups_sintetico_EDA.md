# EDA - powerups_sintetico.csv

## Resumen

- Filas totales: `99`
- Duplicados exactos: `2`
- Conflictos de etiqueta para las mismas features: `0`

## Balance de clases

- `star`: `35`
- `banana`: `29`
- `shell`: `27`
- `mushroom`: `8`

## Variable `recta`

- `true`: `50`
- `false`: `49`

Distribucion por clase:

- `banana`: `15` en recta, `14` en curva
- `shell`: `13` en recta, `14` en curva
- `mushroom`: `8` en recta, `0` en curva
- `star`: `14` en recta, `21` en curva

## Estadisticos globales de features

- `enemigos_delante`: media `1.13`, min `0`, max `5`
- `enemigos_atras`: media `1.17`, min `0`, max `5`
- `platanos_delante`: media `0.75`, min `0`, max `4`
- `conchas_atras`: media `0.75`, min `0`, max `4`

## Medias por clase

### banana

- filas: `29`
- recta true: `15`
- enemigos delante: `0.52`
- enemigos atras: `2.52`
- platanos delante: `0.24`
- conchas atras: `0.34`

### shell

- filas: `27`
- recta true: `13`
- enemigos delante: `2.48`
- enemigos atras: `0.48`
- platanos delante: `0.33`
- conchas atras: `0.22`

### mushroom

- filas: `8`
- recta true: `8`
- enemigos delante: `1.00`
- enemigos atras: `1.00`
- platanos delante: `0.25`
- conchas atras: `0.25`

### star

- filas: `35`
- recta true: `14`
- enemigos delante: `0.63`
- enemigos atras: `0.63`
- platanos delante: `1.60`
- conchas atras: `1.60`

## Lectura rapida

- `banana` esta bien separada por `enemigos_atras` altos.
- `shell` esta bien separada por `enemigos_delante` altos.
- `star` esta bien separada por hazards altos.
- `mushroom` esta poco representada y depende demasiado de `recta=true`.

## Problemas del dataset

- `mushroom` esta desbalanceada: solo `8` ejemplos.
- Hay `2` filas duplicadas exactas.
- No existe la clase `none`, asi que el modelo siempre aprendera a tirar algo.
- Algunas clases comparten contextos mixtos, asi que un modelo simple podria tender a confundir `banana` y `shell` cuando haya enemigos en ambos lados.

## Recomendaciones

- Anadir mas ejemplos de `mushroom`.
- Anadir una clase `none`.
- Eliminar o reducir duplicados.
- Si vas a entrenar un `RandomForest`, usar `class_weight="balanced"` o balancear el dataset antes.
