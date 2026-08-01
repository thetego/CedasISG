import*as React from'react';import{cn}from'@/lib/utils';
export function Card({className,...p}:React.HTMLAttributes<HTMLDivElement>){return <div className={cn('rounded-2xl border border-slate-200/80 bg-white shadow-[0_1px_2px_rgba(15,23,42,.03)]',className)} {...p}/>}
export function CardHeader({className,...p}:React.HTMLAttributes<HTMLDivElement>){return <div className={cn('flex items-start justify-between gap-4 p-5 pb-3',className)} {...p}/>}
export function CardTitle({className,...p}:React.HTMLAttributes<HTMLHeadingElement>){return <h3 className={cn('text-sm font-semibold text-slate-950',className)} {...p}/>}
export function CardContent({className,...p}:React.HTMLAttributes<HTMLDivElement>){return <div className={cn('p-5 pt-2',className)} {...p}/>}
